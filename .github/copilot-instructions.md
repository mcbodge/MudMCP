# MudBlazor.Mcp - AI Coding Agent Instructions

## Project Overview

MudBlazor.Mcp is an MCP (Model Context Protocol) server that provides AI assistants with access to MudBlazor component documentation. It clones the MudBlazor repository, parses source files using Roslyn, and exposes an indexed API via MCP tools.

**Tech Stack:** .NET 10, ASP.NET Core, Roslyn, LibGit2Sharp, Aspire 13.1, xUnit + Moq

## Architecture

```
MCP Tools (12 tools) → ComponentIndexer → Parsing Services → GitRepositoryService
                              ↓
                      In-memory index of ~85 components
```

**Key services:**
- [ComponentIndexer.cs](../src/MudBlazor.Mcp/Services/ComponentIndexer.cs) - Builds/queries the component index
- [XmlDocParser.cs](../src/MudBlazor.Mcp/Services/Parsing/XmlDocParser.cs) - Parses C# source using Roslyn
- [GitRepositoryService.cs](../src/MudBlazor.Mcp/Services/GitRepositoryService.cs) - Clones/updates MudBlazor repo
- [Tools/](../src/MudBlazor.Mcp/Tools/) - MCP tool implementations with `[McpServerTool]` attributes

## Build & Test Commands

```bash
# Build (from repo root)
dotnet build

# Run tests
dotnet test --no-build

# Run server (HTTP transport on localhost:5180)
cd src/MudBlazor.Mcp && dotnet run

# Run server (stdio transport for CLI clients)
cd src/MudBlazor.Mcp && dotnet run -- --stdio

# Run with Aspire dashboard (OpenTelemetry, health checks, service discovery)
cd src/MudBlazor.Mcp.AppHost && dotnet run
```

## Configuration

Configuration via `appsettings.json` with these key sections:

```json
{
  "MudBlazor": {
    "Repository": {
      "Url": "https://github.com/MudBlazor/MudBlazor.git",
      "Branch": "dev",
      "LocalPath": "./data/mudblazor-repo"
    },
    "Cache": {
      "RefreshIntervalMinutes": 60,
      "ComponentCacheDurationMinutes": 30,
      "ExampleCacheDurationMinutes": 120
    },
    "Parsing": {
      "IncludeInternalComponents": false,
      "IncludeDeprecatedComponents": true,
      "MaxExamplesPerComponent": 20
    }
  }
}
```

Options are bound to strongly-typed classes in [Configuration/MudBlazorOptions.cs](../src/MudBlazor.Mcp/Configuration/MudBlazorOptions.cs).

## Aspire Integration (13.1)

The project uses .NET Aspire for orchestration and observability:

**AppHost** ([MudBlazor.Mcp.AppHost](../src/MudBlazor.Mcp.AppHost/Program.cs)):
```csharp
var builder = DistributedApplication.CreateBuilder(args);
builder.AddProject<Projects.MudBlazor_Mcp>("mudblazor-mcp");
builder.Build().Run();
```

**ServiceDefaults** ([Extensions.cs](../src/MudBlazor.Mcp.ServiceDefaults/Extensions.cs)) provides:
- OpenTelemetry (metrics, tracing, logging)
- Health checks (`/health`, `/alive`)
- Service discovery with resilience handlers
- OTLP exporter when `OTEL_EXPORTER_OTLP_ENDPOINT` is set

To add Aspire defaults to a service: `builder.AddServiceDefaults()`.

## Roslyn Parsing Deep Dive

The parsing layer extracts component metadata from C# source using Roslyn's syntax API.

### XmlDocParser - Core C# Parsing

[XmlDocParser.cs](../src/MudBlazor.Mcp/Services/Parsing/XmlDocParser.cs) extracts component info:

```csharp
// Parse syntax tree from source
var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
var root = syntaxTree.GetRoot();

// Find public class declaration
var classDeclaration = root.DescendantNodes()
    .OfType<ClassDeclarationSyntax>()
    .FirstOrDefault(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));
```

**Key extraction patterns:**
- **Parameters**: Properties with `[Parameter]` or `[CascadingParameter]` attributes
- **Events**: Properties of type `EventCallback` or `EventCallback<T>` with `[Parameter]`
- **Methods**: Public methods excluding lifecycle overrides (`OnInitialized`, `Dispose`, etc.)
- **XML docs**: Extracted from `DocumentationCommentTriviaSyntax` leading trivia

**Regex patterns** (generated via `[GeneratedRegex]` for performance):
- `CategoryTypesRegex` - Extracts `CategoryTypes.xxx` values
- `GenericArgumentRegex` - Extracts `<T>` from `EventCallback<T>`

### RazorDocParser - Documentation Extraction

[RazorDocParser.cs](../src/MudBlazor.Mcp/Services/Parsing/RazorDocParser.cs) parses `*Page.razor` files:
- Extracts `Title` and `SubTitle` from `<DocsPageHeader>` components
- Finds `<DocsPageSection>` blocks for structured content
- Identifies related components via `href="/components/..."` links

### ExampleExtractor - Code Examples

[ExampleExtractor.cs](../src/MudBlazor.Mcp/Services/Parsing/ExampleExtractor.cs) finds examples in:
`Docs/Pages/Components/{ComponentName}/*Example*.razor`

Splits files into markup and `@code` blocks, cleans directives (`@page`, `@using`).

### CategoryMapper - Component Organization

[CategoryMapper.cs](../src/MudBlazor.Mcp/Services/Parsing/CategoryMapper.cs) maps components to categories:
- Hardcoded category definitions from MudBlazor's `MenuService`
- Pattern-based inference: `*Button*` → "Buttons", `*Field*` → "Form Inputs"

## Code Patterns

### MCP Tools Pattern
Tools are static methods with DI parameters and `[McpServerTool]` + `[Description]` attributes:

```csharp
[McpServerToolType]
public sealed class ComponentDetailTools
{
    [McpServerTool(Name = "get_component_detail")]
    [Description("Gets comprehensive details about a MudBlazor component.")]
    public static async Task<string> GetComponentDetailAsync(
        IComponentIndexer indexer,           // DI injected
        ILogger<ComponentDetailTools> logger,
        [Description("Component name")] string componentName,  // Tool parameter
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(componentName, nameof(componentName));
        // ... implementation
    }
}
```

### Error Handling in Tools
Use `ToolValidation` for consistent MCP-friendly errors that LLMs can self-correct:

```csharp
ToolValidation.RequireNonEmpty(componentName, nameof(componentName));
ToolValidation.ThrowComponentNotFound(componentName);  // Suggests list_components
```

### Domain Models
All models in [Models/ComponentInfo.cs](../src/MudBlazor.Mcp/Models/ComponentInfo.cs) are immutable records:
- `ComponentInfo` - Component with parameters, events, methods, examples
- `ComponentParameter`, `ComponentEvent`, `ComponentMethod`, `ComponentExample`
- `ApiReference`, `ComponentCategory`

## Adding a New MCP Tool (Step-by-Step)

### 1. Create the Tool Class
Add a new file in `src/MudBlazor.Mcp/Tools/`:

```csharp
// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0.

using System.ComponentModel;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Tools;

[McpServerToolType]
public sealed class MyNewTools
{
    [McpServerTool(Name = "my_new_tool")]
    [Description("Describe what this tool does for LLMs.")]
    public static async Task<string> MyNewToolAsync(
        IComponentIndexer indexer,              // Inject services
        ILogger<MyNewTools> logger,
        [Description("Parameter description")] string requiredParam,
        [Description("Optional param")] int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate inputs
        ToolValidation.RequireNonEmpty(requiredParam, nameof(requiredParam));
        ToolValidation.RequireInRange(maxResults, 1, 100, nameof(maxResults));

        // 2. Call indexer/services
        var result = await indexer.SomeMethodAsync(cancellationToken);

        // 3. Format output as markdown (LLM-friendly)
        return $"# Results\n\n{FormatResult(result)}";
    }
}
```

### 2. Write Unit Tests
Add test file in `tests/MudBlazor.Mcp.Tests/Tools/`:

```csharp
public class MyNewToolsTests
{
    private static readonly ILogger<MyNewTools> NullLogger = 
        NullLoggerFactory.Instance.CreateLogger<MyNewTools>();

    [Fact]
    public async Task MyNewToolAsync_WithValidInput_ReturnsExpectedResult()
    {
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.SomeMethodAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        var result = await MyNewTools.MyNewToolAsync(
            indexer.Object, NullLogger, "test", 10, CancellationToken.None);

        Assert.Contains("expected", result);
    }

    [Fact]
    public async Task MyNewToolAsync_WithEmptyParam_ThrowsMcpException()
    {
        var indexer = new Mock<IComponentIndexer>();

        await Assert.ThrowsAsync<McpException>(() =>
            MyNewTools.MyNewToolAsync(indexer.Object, NullLogger, "", 10, CancellationToken.None));
    }
}
```

### 3. Build & Test
```bash
dotnet build
dotnet test --no-build
```

### 4. Test Manually
```bash
cd src/MudBlazor.Mcp && dotnet run
# In another terminal:
curl -X POST http://localhost:5180/mcp \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","method":"tools/call","params":{"name":"my_new_tool","arguments":{"requiredParam":"test"}},"id":1}'
```

**No registration needed** - tools are auto-discovered via `WithToolsFromAssembly()`.

## Testing Conventions

- Tests in `tests/MudBlazor.Mcp.Tests/` mirror `src/` structure
- Use Moq for interface mocking, xUnit for assertions
- Use `NullLoggerFactory.Instance.CreateLogger<T>()` for test loggers
- Tool tests should verify both success and `McpException` error cases

Example pattern from [ComponentDetailToolsTests.cs](../tests/MudBlazor.Mcp.Tests/Tools/ComponentDetailToolsTests.cs):
```csharp
[Fact]
public async Task GetComponentDetailAsync_WithInvalidComponent_ThrowsMcpException()
{
    var indexer = new Mock<IComponentIndexer>();
    indexer.Setup(x => x.GetComponentAsync("Unknown", It.IsAny<CancellationToken>()))
        .ReturnsAsync((ComponentInfo?)null);

    var ex = await Assert.ThrowsAsync<McpException>(...);
    Assert.Contains("not found", ex.Message);
}
```

## Key Files to Understand

| Purpose | Location |
|---------|----------|
| Startup/DI | [Program.cs](../src/MudBlazor.Mcp/Program.cs) |
| Service interfaces | [Services/IComponentIndexer.cs](../src/MudBlazor.Mcp/Services/IComponentIndexer.cs) |
| Roslyn parsing | [Services/Parsing/XmlDocParser.cs](../src/MudBlazor.Mcp/Services/Parsing/XmlDocParser.cs) |
| Example extraction | [Services/Parsing/ExampleExtractor.cs](../src/MudBlazor.Mcp/Services/Parsing/ExampleExtractor.cs) |
| Category mapping | [Services/Parsing/CategoryMapper.cs](../src/MudBlazor.Mcp/Services/Parsing/CategoryMapper.cs) |
| Configuration | [Configuration/MudBlazorOptions.cs](../src/MudBlazor.Mcp/Configuration/MudBlazorOptions.cs) |
| Tool validation | [Tools/ToolValidation.cs](../src/MudBlazor.Mcp/Tools/ToolValidation.cs) |
| Aspire host | [MudBlazor.Mcp.AppHost/Program.cs](../src/MudBlazor.Mcp.AppHost/Program.cs) |

## Project-Specific Notes

- The `data/mudblazor-repo/` folder is cloned at runtime and excluded from compilation via `<DefaultItemExcludes>`
- Server supports both HTTP (`/mcp` endpoint) and stdio transports via `--stdio` flag
- Health checks at `/health`, `/health/ready`, `/health/live`
- All logging goes to stderr for MCP protocol compatibility
- Component names support flexible lookup: "Button" resolves to "MudButton"
- Aspire SDK version is pinned in `MudBlazor.Mcp.AppHost.csproj`: `<Sdk Name="Aspire.AppHost.Sdk" Version="13.1.0" />`

## License

GPL-2.0 - Include copyright header in all source files:
```csharp
// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0.
```
