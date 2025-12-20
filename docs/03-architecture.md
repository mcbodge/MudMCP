# Architecture

This document provides a deep technical dive into the Mud MCP architecture, including component design, data flow, and implementation details.

## Table of Contents

- [System Architecture](#system-architecture)
- [Layer Design](#layer-design)
- [Core Components](#core-components)
- [Data Flow](#data-flow)
- [Domain Models](#domain-models)
- [MCP Protocol Implementation](#mcp-protocol-implementation)
- [Roslyn Parsing Pipeline](#roslyn-parsing-pipeline)
- [Caching Strategy](#caching-strategy)
- [Aspire Integration](#aspire-integration)

---

## System Architecture

### High-Level Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            MCP Clients                                   │
│    ┌──────────────┐    ┌──────────────┐    ┌──────────────┐            │
│    │GitHub Copilot│    │Claude Desktop│    │ MCP Inspector│            │
│    └──────────────┘    └──────────────┘    └──────────────┘            │
└─────────────────────────────────────────────────────────────────────────┘
                │ JSON-RPC over HTTP/stdio            │
                ▼                                     ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                            Mud MCP Server                                │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    Transport Layer                                │   │
│  │              HTTP Transport │ stdio Transport                     │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                 │                                       │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                    MCP Protocol Handler                          │   │
│  │         Tool Registration │ Request Routing │ Response Format    │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                 │                                       │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                       Tool Layer                                  │   │
│  │  ComponentListTools │ ComponentDetailTools │ ComponentSearchTools│   │
│  │  ComponentExampleTools │ ApiReferenceTools                       │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                 │                                       │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                     Service Layer                                 │   │
│  │                    ComponentIndexer                               │   │
│  │         IDocumentationCache │ IGitRepositoryService              │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                 │                                       │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                     Parsing Layer                                 │   │
│  │  XmlDocParser │ RazorDocParser │ ExampleExtractor │ CategoryMapper│   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                 │                                       │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                  Infrastructure Layer                             │   │
│  │              GitRepositoryService (LibGit2Sharp)                  │   │
│  │                     File System │ Memory Cache                    │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────┘
                                 │
                                 ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                     MudBlazor Repository                                 │
│                    (Cloned from GitHub)                                  │
│   src/MudBlazor/Components/  │  src/MudBlazor.Docs/Pages/Components/   │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Layer Design

### Transport Layer

Supports two transport mechanisms:

| Transport | Use Case | Configuration |
|-----------|----------|---------------|
| **HTTP** | Web-based clients, MCP Inspector | Default on port 5180 |
| **stdio** | CLI clients, Claude Desktop | `--stdio` flag |

```csharp
if (useStdio)
{
    builder.Services.AddMcpServer(options => { /* ... */ })
        .WithStdioServerTransport()
        .WithToolsFromAssembly();
}
else
{
    builder.Services.AddMcpServer(options => { /* ... */ })
        .WithHttpTransport()
        .WithToolsFromAssembly();
}
```

### MCP Protocol Handler

The `ModelContextProtocol.AspNetCore` package handles:
- JSON-RPC message parsing
- Tool discovery and registration
- Request routing to tool methods
- Response serialization

### Tool Layer

Static methods decorated with MCP attributes:

```csharp
[McpServerToolType]
public sealed class ComponentListTools
{
    [McpServerTool(Name = "list_components")]
    [Description("Lists all available MudBlazor components.")]
    public static async Task<string> ListComponentsAsync(
        IComponentIndexer indexer,           // DI injected
        ILogger<ComponentListTools> logger,  // DI injected
        [Description("Optional category filter")]
        string? category = null,             // Tool parameter
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

### Service Layer

Business logic and data management:

| Service | Responsibility |
|---------|----------------|
| `IComponentIndexer` | Builds and queries the component index |
| `IDocumentationCache` | Caches parsed documentation |
| `IGitRepositoryService` | Manages Git operations |

### Parsing Layer

Roslyn-based source code analysis:

| Parser | Input | Output |
|--------|-------|--------|
| `XmlDocParser` | `.cs` files | Parameters, events, methods |
| `RazorDocParser` | `*Page.razor` files | Descriptions, sections |
| `ExampleExtractor` | `*Example.razor` files | Code examples |
| `CategoryMapper` | Component names | Category assignments |

---

## Core Components

### ComponentIndexer

The central service that coordinates all documentation operations:

```csharp
public sealed class ComponentIndexer : IComponentIndexer
{
    // In-memory indexes
    private readonly ConcurrentDictionary<string, ComponentInfo> _components;
    private readonly ConcurrentDictionary<string, ApiReference> _apiReferences;
    
    // State
    public bool IsIndexed { get; }
    public DateTimeOffset? LastIndexed { get; }
    
    // Operations
    public Task BuildIndexAsync(CancellationToken ct);
    public Task<ComponentInfo?> GetComponentAsync(string name, CancellationToken ct);
    public Task<IReadOnlyList<ComponentInfo>> SearchComponentsAsync(...);
}
```

**Index Building Process:**

```
┌──────────────────┐
│ EnsureRepository │─────▶ Clone or update Git repo
└────────┬─────────┘
         │
┌────────▼─────────┐
│InitializeCategories│─────▶ Load known category mappings
└────────┬─────────┘
         │
┌────────▼─────────┐
│  IndexComponents │─────▶ Parse all Mud*.cs files
└────────┬─────────┘
         │
┌────────▼─────────┐
│IndexDocumentation│─────▶ Parse *Page.razor files
└────────┬─────────┘
         │
┌────────▼─────────┐
│   IndexExamples  │─────▶ Parse *Example.razor files
└────────┬─────────┘
         │
┌────────▼─────────┐
│   Index Ready    │─────▶ ~85 components indexed
└──────────────────┘
```

### GitRepositoryService

Manages the MudBlazor repository clone:

```csharp
public sealed class GitRepositoryService : IGitRepositoryService
{
    public string RepositoryPath { get; }
    public bool IsAvailable { get; }
    public string? CurrentCommitHash { get; }
    
    public Task<bool> EnsureRepositoryAsync(CancellationToken ct);
    public Task ForceRefreshAsync(CancellationToken ct);
}
```

**Key behaviors:**
- Clones on first run if repository doesn't exist
- Fetches and hard resets to remote branch on updates
- Uses `LibGit2Sharp` for native Git operations
- Thread-safe with `SemaphoreSlim`

---

## Data Flow

### Tool Invocation Flow

```
┌─────────┐  JSON-RPC   ┌───────────┐  Method Call  ┌──────────┐
│ Client  │────────────▶│MCP Handler│──────────────▶│Tool Class│
└─────────┘             └───────────┘               └────┬─────┘
                                                         │ DI
                                                   ┌─────▼─────┐
                                                   │ Indexer   │
                                                   └─────┬─────┘
                                                         │ Query
                                                   ┌─────▼─────┐
                                                   │In-Memory  │
                                                   │  Index    │
                                                   └─────┬─────┘
                                                         │ Format
                                                   ┌─────▼─────┐
                                                   │ Markdown  │
                                                   │  Output   │
                                                   └─────┬─────┘
                                                         │
┌─────────┐  JSON-RPC   ┌───────────┐  Return      ┌────▼─────┐
│ Client  │◀────────────│MCP Handler│◀─────────────│  Result  │
└─────────┘             └───────────┘              └──────────┘
```

### Index Building Flow

```
┌──────────────────────────────────────────────────────────────┐
│                     Repository                                │
│  src/MudBlazor/Components/                                   │
│    Button/MudButton.razor.cs ───┐                            │
│    TextField/MudTextField.cs ───┼─▶ XmlDocParser             │
│    ...                       ───┘                             │
│                                                               │
│  src/MudBlazor.Docs/Pages/Components/                        │
│    Button/ButtonPage.razor ─────┐                            │
│    Button/ButtonBasicExample.razor ─┼─▶ RazorDocParser       │
│    ...                          ─┘    ExampleExtractor       │
└──────────────────────────────────────────────────────────────┘
                        │
                        ▼
┌──────────────────────────────────────────────────────────────┐
│                   ComponentIndexer                            │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │ ConcurrentDictionary<string, ComponentInfo>              │ │
│  │   "MudButton" → ComponentInfo(...)                       │ │
│  │   "MudTextField" → ComponentInfo(...)                    │ │
│  │   ...                                                    │ │
│  └─────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
```

---

## Domain Models

All models are immutable C# records:

### ComponentInfo

```csharp
public sealed record ComponentInfo(
    string Name,                              // "MudButton"
    string Namespace,                         // "MudBlazor"
    string Summary,                           // "A Material Design button"
    string? Description,                      // Extended description
    string? Category,                         // "Buttons"
    string? BaseType,                         // "MudBaseButton"
    IReadOnlyList<ComponentParameter> Parameters,
    IReadOnlyList<ComponentEvent> Events,
    IReadOnlyList<ComponentMethod> Methods,
    IReadOnlyList<ComponentExample> Examples,
    IReadOnlyList<string> RelatedComponents,
    string? DocumentationUrl,
    string? SourceUrl
);
```

### ComponentParameter

```csharp
public sealed record ComponentParameter(
    string Name,           // "Color"
    string Type,           // "Color"
    string? Description,   // "The color of the button"
    string? DefaultValue,  // "Color.Default"
    bool IsRequired,       // false
    bool IsCascading,      // false
    string? Category       // "Appearance"
);
```

### Model Hierarchy

```
ComponentInfo
├── Parameters: List<ComponentParameter>
├── Events: List<ComponentEvent>
├── Methods: List<ComponentMethod>
│   └── Parameters: List<MethodParameter>
└── Examples: List<ComponentExample>
    └── Features: List<string>

ComponentCategory
└── ComponentNames: List<string>

ApiReference
├── Members: List<ApiMember>
└── EnumValues: List<EnumValue>
```

---

## MCP Protocol Implementation

### Tool Registration

Tools are discovered automatically via reflection:

```csharp
builder.Services.AddMcpServer(options =>
{
    options.ServerInfo = new()
    {
        Name = "MudBlazor Documentation Server",
        Version = "1.0.0"
    };
})
.WithHttpTransport()
.WithToolsFromAssembly();  // Scans for [McpServerToolType]
```

### Tool Attributes

```csharp
[McpServerToolType]        // Marks class as containing tools
[McpServerTool(Name = "tool_name")]  // Marks method as a tool
[Description("...")]       // Used by LLMs to understand the tool
```

### Parameter Injection

Tools can inject services and receive parameters:

```csharp
public static async Task<string> MyToolAsync(
    // DI-injected services (no attributes needed)
    IComponentIndexer indexer,
    ILogger<MyTools> logger,
    
    // Tool parameters (require [Description])
    [Description("The component name")]
    string componentName,
    
    [Description("Max results (default: 10)")]
    int maxResults = 10,
    
    // CancellationToken (automatically populated)
    CancellationToken cancellationToken = default)
```

### Error Handling

Use `McpException` for protocol-level errors:

```csharp
if (component is null)
{
    throw new McpException(
        $"Component '{name}' not found. Use 'list_components' to see available components.");
}
```

---

## Roslyn Parsing Pipeline

### XmlDocParser

Extracts component metadata from C# source:

```csharp
public ComponentParseResult? ParseSourceCode(string sourceCode, string filePath)
{
    // 1. Parse syntax tree
    var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
    var root = syntaxTree.GetRoot();
    
    // 2. Find public class
    var classDeclaration = root.DescendantNodes()
        .OfType<ClassDeclarationSyntax>()
        .FirstOrDefault(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));
    
    // 3. Extract XML documentation
    var xmlDoc = ExtractXmlDocumentation(classDeclaration);
    
    // 4. Extract parameters ([Parameter] attributed properties)
    var parameters = ExtractParameters(classDeclaration);
    
    // 5. Extract events (EventCallback properties)
    var events = ExtractEvents(classDeclaration);
    
    // 6. Extract public methods
    var methods = ExtractPublicMethods(classDeclaration);
    
    return new ComponentParseResult { /* ... */ };
}
```

### Parameter Detection

```csharp
// Looks for [Parameter] or [CascadingParameter] attributes
var hasParameterAttribute = property.AttributeLists
    .SelectMany(al => al.Attributes)
    .Any(a => a.Name.ToString() is "Parameter" or "CascadingParameter");
```

### Event Detection

```csharp
// Looks for EventCallback or EventCallback<T> types with [Parameter]
var typeName = property.Type.ToString();
if (typeName.StartsWith("EventCallback") && hasParameterAttribute)
{
    // Extract event args type from generic
    var eventArgsType = ExtractGenericArgument(typeName);
}
```

---

## Caching Strategy

### In-Memory Index

The primary cache is the `ConcurrentDictionary` in `ComponentIndexer`:

```csharp
private readonly ConcurrentDictionary<string, ComponentInfo> _components;
```

**Characteristics:**
- Thread-safe for concurrent reads
- Rebuilt on server restart
- ~85 components, minimal memory footprint

### Documentation Cache

`IDocumentationCache` provides additional caching:

```csharp
public interface IDocumentationCache
{
    Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null);
    void Remove(string key);
    void Clear();
}
```

**Configuration:**
```json
{
  "Cache": {
    "SlidingExpirationMinutes": 60,
    "AbsoluteExpirationMinutes": 1440
  }
}
```

---

## Aspire Integration

### ServiceDefaults

The `MudBlazor.Mcp.ServiceDefaults` project provides:

```csharp
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.ConfigureOpenTelemetry();      // Metrics, traces, logs
    builder.AddDefaultHealthChecks();      // Health endpoints
    builder.Services.AddServiceDiscovery(); // Service mesh support
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler(); // Retry policies
        http.AddServiceDiscovery();
    });
    return builder;
}
```

### AppHost

Orchestration via Aspire:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.MudBlazor_Mcp>("mudblazor-mcp");
builder.Build().Run();
```

### OpenTelemetry

Automatic instrumentation:
- **Metrics**: ASP.NET Core, HTTP client, runtime
- **Traces**: Request traces with spans
- **Logs**: Structured logging to OTLP

Export to OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

---

## Health Checks

### Custom Health Check

```csharp
public class IndexerHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        if (!_indexer.IsIndexed)
            return HealthCheckResult.Degraded("Index not yet built");
        
        var components = await _indexer.GetAllComponentsAsync(cancellationToken);
        
        if (components.Count == 0)
            return HealthCheckResult.Degraded("Index is empty");
        
        return HealthCheckResult.Healthy(
            $"Index contains {components.Count} components",
            data: new Dictionary<string, object>
            {
                ["componentCount"] = components.Count,
                ["lastIndexed"] = _indexer.LastIndexed
            });
    }
}
```

### Endpoints

| Endpoint | Purpose |
|----------|---------|
| `/health` | Overall health (all checks) |
| `/health/ready` | Readiness (index built) |
| `/health/live` | Liveness (server running) |

---

## Next Steps

- [Best Practices](./04-best-practices.md) — Design patterns and implementation practices
- [Tools Reference](./05-tools-reference.md) — Detailed tool documentation
- [Configuration](./06-configuration.md) — All configuration options
