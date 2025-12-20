# Best Practices

This document details the best practices implemented in the Mud MCP server, providing guidance for understanding, maintaining, and extending the codebase.

## Table of Contents

- [Error Handling](#error-handling)
- [Resource Management](#resource-management)
- [Security Considerations](#security-considerations)
- [Performance Optimization](#performance-optimization)
- [Logging and Observability](#logging-and-observability)
- [Code Organization](#code-organization)
- [Design Patterns](#design-patterns)

---

## Error Handling

### MCP Protocol Errors

All tool validation errors are thrown as `McpException` so that LLMs can see and self-correct:

```csharp
// Good: Use ToolValidation for consistent, LLM-friendly errors
ToolValidation.RequireNonEmpty(componentName, nameof(componentName));
ToolValidation.ThrowComponentNotFound(componentName);

// The error message includes recovery guidance:
// "Component 'Unknown' not found. Use 'list_components' to see available components."
```

### Validation Helper Class

The `ToolValidation` class centralizes all validation logic:

```csharp
public static class ToolValidation
{
    // Required parameter validation
    public static void RequireNonEmpty([NotNull] string? value, string parameterName);
    
    // Range validation
    public static void RequireInRange(int value, int min, int max, string parameterName);
    
    // Enum/option validation
    public static void RequireValidOption(string? value, string[] allowedValues, string parameterName);
    
    // Domain-specific errors with helpful messages
    [DoesNotReturn]
    public static void ThrowComponentNotFound(string componentName);
    
    [DoesNotReturn]
    public static void ThrowCategoryNotFound(string categoryName, IEnumerable<string>? available = null);
}
```

### Graceful Degradation

The server continues operating even when some operations fail:

```csharp
// In ComponentIndexer - parsing failures don't stop the index build
catch (Exception ex) when (ex is not OperationCanceledException)
{
    _logger.LogWarning(ex, "Failed to parse component in: {Dir}", dirName);
    // Continue processing other components
}
```

### Exception Filtering

Using exception filters for clean error categorization:

```csharp
try
{
    // Git operations
}
catch (IOException ex)
{
    _logger.LogError(ex, "IO error while ensuring repository");
    throw;
}
catch (UnauthorizedAccessException ex)
{
    _logger.LogError(ex, "Access denied while ensuring repository");
    throw;
}
catch (LibGit2SharpException ex)
{
    _logger.LogError(ex, "Git operation failed");
    throw;
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    _logger.LogError(ex, "Unexpected error");
    throw;
}
```

---

## Resource Management

### Async Disposable Pattern

The `GitRepositoryService` implements both `IDisposable` and `IAsyncDisposable`:

```csharp
public sealed class GitRepositoryService : IGitRepositoryService, IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private Repository? _repository;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _repository?.Dispose();
        _syncLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        
        await _syncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _repository?.Dispose();
        }
        finally
        {
            _syncLock.Release();
            _syncLock.Dispose();
        }
    }
}
```

### Thread Safety

Using `SemaphoreSlim` for async-compatible locking:

```csharp
private readonly SemaphoreSlim _indexLock = new(1, 1);

public async Task BuildIndexAsync(CancellationToken cancellationToken = default)
{
    await _indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
    try
    {
        // Index building logic
    }
    finally
    {
        _indexLock.Release();
    }
}
```

### Concurrent Collections

Using `ConcurrentDictionary` for thread-safe in-memory storage:

```csharp
private readonly ConcurrentDictionary<string, ComponentInfo> _components = 
    new(StringComparer.OrdinalIgnoreCase);
```

### Cancellation Token Propagation

All async operations support cancellation:

```csharp
public async Task<ComponentInfo?> GetComponentAsync(
    string componentName, 
    CancellationToken cancellationToken = default)
{
    cancellationToken.ThrowIfCancellationRequested();
    // ...
}
```

---

## Security Considerations

### Input Validation

All tool parameters are validated before use:

```csharp
[McpServerTool(Name = "search_components")]
public static async Task<string> SearchComponentsAsync(
    // ...
    string query,
    int maxResults = 10)
{
    // Validate inputs immediately
    ToolValidation.RequireNonEmpty(query, nameof(query));
    ToolValidation.RequireInRange(maxResults, 1, 50, nameof(maxResults));
    
    // Safe to proceed
}
```

### Path Traversal Prevention

Repository paths are validated and constrained:

```csharp
public string GetPath(string relativePath)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
    return Path.Combine(RepositoryPath, 
        relativePath.Replace('/', Path.DirectorySeparatorChar));
}
```

### No Direct File System Exposure

Tools never expose raw file paths to clients; they work with abstracted component names:

```csharp
// Good: Abstracted component access
await indexer.GetComponentAsync("MudButton", ct);

// Not exposed: Direct file path
// File.ReadAllText("/path/to/MudButton.cs");
```

### Production Security Recommendations

The README includes guidance for production deployments:

```csharp
// Rate limiting example
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        httpContext => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

---

## Performance Optimization

### Parallel Processing

Component directories are indexed in parallel:

```csharp
var tasks = componentDirs.Select(dir => 
    IndexComponentDirectoryAsync(dir, cancellationToken));
await Task.WhenAll(tasks).ConfigureAwait(false);
```

### Source Generator Regex

Using source-generated regex for better performance:

```csharp
public sealed partial class XmlDocParser
{
    [GeneratedRegex(@"^\s*///\s?", RegexOptions.Multiline)]
    private static partial Regex XmlCommentPrefixRegex();

    [GeneratedRegex(@"CategoryTypes\.(\w+)")]
    private static partial Regex CategoryTypesRegex();
}
```

### Lazy Loading

Components are indexed once and served from memory:

```csharp
// Index built once at startup
await indexer.BuildIndexAsync();

// All queries served from memory
var component = await indexer.GetComponentAsync("MudButton", ct);
```

### String Building

Using `StringBuilder` for efficient string concatenation:

```csharp
var sb = new StringBuilder();
sb.AppendLine($"# {component.Name}");
sb.AppendLine();
sb.AppendLine("## Parameters");
foreach (var param in component.Parameters)
{
    sb.AppendLine($"- `{param.Name}`: {param.Type}");
}
return sb.ToString();
```

### ConfigureAwait(false)

Library code uses `ConfigureAwait(false)` to avoid context switching:

```csharp
await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
```

---

## Logging and Observability

### Structured Logging

Using semantic logging with message templates:

```csharp
_logger.LogInformation(
    "Index build completed in {ElapsedMs}ms. Indexed {Count} components",
    sw.ElapsedMilliseconds, 
    _components.Count);
```

### Log Levels

Appropriate log levels for different scenarios:

| Level | Use Case |
|-------|----------|
| `Trace` | Detailed diagnostic info |
| `Debug` | Development troubleshooting |
| `Information` | General operational events |
| `Warning` | Potential issues, recoverable errors |
| `Error` | Errors that need attention |

```csharp
_logger.LogDebug("Getting component detail for: {ComponentName}", componentName);
_logger.LogWarning("Component not found: {ComponentName}", componentName);
_logger.LogError(ex, "Failed to parse file: {FilePath}", filePath);
```

### stderr for MCP Compatibility

Logging configured to stderr for stdio transport:

```csharp
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
```

### Health Check Data

Rich health check data for observability:

```csharp
return HealthCheckResult.Healthy(
    $"Index contains {components.Count} components in {categories.Count} categories.",
    data: new Dictionary<string, object>
    {
        ["status"] = "ready",
        ["componentCount"] = components.Count,
        ["categoryCount"] = categories.Count,
        ["isIndexed"] = true,
        ["lastIndexed"] = _indexer.LastIndexed?.ToString("O") ?? "never"
    });
```

### OpenTelemetry Integration

Aspire ServiceDefaults enables automatic telemetry:

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource(builder.Environment.ApplicationName)
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation();
    });
```

---

## Code Organization

### Project Structure

```
src/MudBlazor.Mcp/
├── Configuration/          # Options classes
│   └── MudBlazorOptions.cs
├── Models/                 # Domain models (records)
│   └── ComponentInfo.cs
├── Services/               # Business logic
│   ├── ComponentIndexer.cs
│   ├── GitRepositoryService.cs
│   └── Parsing/           # Parsing utilities
│       ├── XmlDocParser.cs
│       ├── RazorDocParser.cs
│       ├── ExampleExtractor.cs
│       └── CategoryMapper.cs
└── Tools/                  # MCP tools
    ├── ComponentListTools.cs
    ├── ComponentDetailTools.cs
    ├── ComponentSearchTools.cs
    ├── ComponentExampleTools.cs
    ├── ApiReferenceTools.cs
    └── ToolValidation.cs
```

### Interface Segregation

Each service has a focused interface:

```csharp
public interface IComponentIndexer
{
    bool IsIndexed { get; }
    DateTimeOffset? LastIndexed { get; }
    Task BuildIndexAsync(CancellationToken ct = default);
    Task<ComponentInfo?> GetComponentAsync(string name, CancellationToken ct = default);
    // ...
}

public interface IGitRepositoryService
{
    string RepositoryPath { get; }
    bool IsAvailable { get; }
    Task<bool> EnsureRepositoryAsync(CancellationToken ct = default);
    // ...
}
```

### Separation by Feature

Tools are organized by functional area:

| Tool Class | Responsibility |
|------------|----------------|
| `ComponentListTools` | Browsing and listing |
| `ComponentDetailTools` | Detailed information |
| `ComponentSearchTools` | Search and discovery |
| `ComponentExampleTools` | Code examples |
| `ApiReferenceTools` | API documentation |

---

## Design Patterns

### Immutable Records

All domain models are immutable:

```csharp
public sealed record ComponentInfo(
    string Name,
    string Namespace,
    // ...
);

// Creating modified copies
var enhanced = component with 
{
    Description = docResult.Description ?? component.Description,
    RelatedComponents = docResult.RelatedComponents
};
```

### Options Pattern

Strongly-typed configuration:

```csharp
public sealed class MudBlazorOptions
{
    public const string SectionName = "MudBlazor";
    
    public RepositoryOptions Repository { get; set; } = new();
    public CacheOptions Cache { get; set; } = new();
    public ParsingOptions Parsing { get; set; } = new();
}

// Registration
builder.Services.Configure<MudBlazorOptions>(
    builder.Configuration.GetSection(MudBlazorOptions.SectionName));

// Injection
public ComponentIndexer(IOptions<MudBlazorOptions> options)
{
    _options = options.Value;
}
```

### Static Tool Methods

MCP tools are static methods with DI parameters:

```csharp
[McpServerToolType]
public sealed class ComponentListTools
{
    [McpServerTool(Name = "list_components")]
    public static async Task<string> ListComponentsAsync(
        IComponentIndexer indexer,  // Injected by DI
        ILogger<ComponentListTools> logger,  // Injected by DI
        string? category = null,  // Tool parameter
        CancellationToken cancellationToken = default)
    {
        // Implementation
    }
}
```

### Factory Pattern (Implicit)

The DI container acts as a factory for services:

```csharp
// Registration (factory configuration)
builder.Services.AddSingleton<IComponentIndexer, ComponentIndexer>();
builder.Services.AddSingleton<XmlDocParser>();

// Resolution (factory usage)
var indexer = app.Services.GetRequiredService<IComponentIndexer>();
```

### Template Method Pattern

Base parsing flow with customization points:

```csharp
public async Task BuildIndexAsync(CancellationToken ct)
{
    await _gitService.EnsureRepositoryAsync(ct);  // Step 1
    await _categoryMapper.InitializeAsync(repoPath, ct);  // Step 2
    await IndexComponentsAsync(repoPath, ct);  // Step 3
    await IndexDocumentationAsync(repoPath, ct);  // Step 4
    await IndexExamplesAsync(repoPath, ct);  // Step 5
}
```

### Strategy Pattern (Search)

Search behavior varies based on field selection:

```csharp
public enum SearchFields
{
    Name = 1,
    Description = 2,
    Parameters = 4,
    Examples = 8,
    All = Name | Description | Parameters | Examples
}

// Strategy selection
var score = CalculateSearchScore(component, query, searchFields);
```

---

## Summary

| Practice | Implementation |
|----------|----------------|
| **Error Handling** | `McpException` with recovery hints, graceful degradation |
| **Resource Management** | `IAsyncDisposable`, `SemaphoreSlim`, `ConcurrentDictionary` |
| **Security** | Input validation, path constraints, no file exposure |
| **Performance** | Parallel processing, source-generated regex, in-memory index |
| **Logging** | Structured logging, semantic templates, stderr for MCP |
| **Code Organization** | Feature-based structure, interface segregation |
| **Design Patterns** | Immutable records, options pattern, DI-injected tools |

---

## Next Steps

- [Tools Reference](./05-tools-reference.md) — Complete tool documentation
- [Configuration](./06-configuration.md) — All configuration options
- [Testing](./07-testing.md) — Unit testing strategy
