// Copyright (c) 2024 MudBlazor.Mcp Contributors
// Licensed under the MIT License.

using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Services.Parsing;

var builder = WebApplication.CreateBuilder(args);

// Configure logging to stderr for MCP compatibility
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Bind configuration
builder.Services.Configure<MudBlazorOptions>(
    builder.Configuration.GetSection("MudBlazor"));
builder.Services.Configure<RepositoryOptions>(
    builder.Configuration.GetSection("MudBlazor:Repository"));
builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection("MudBlazor:Cache"));
builder.Services.Configure<ParsingOptions>(
    builder.Configuration.GetSection("MudBlazor:Parsing"));

// Add memory caching
builder.Services.AddMemoryCache();

// Register services
builder.Services.AddSingleton<IGitRepositoryService, GitRepositoryService>();
builder.Services.AddSingleton<IDocumentationCache, DocumentationCache>();
builder.Services.AddSingleton<IComponentIndexer, ComponentIndexer>();

// Register parsers
builder.Services.AddSingleton<XmlDocParser>();
builder.Services.AddSingleton<RazorDocParser>();
builder.Services.AddSingleton<ExampleExtractor>();
builder.Services.AddSingleton<CategoryMapper>();

// Health checks
builder.Services.AddHealthChecks()
    .AddCheck<IndexerHealthCheck>("indexer");

// Add MCP server with SSE transport
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

var app = builder.Build();

// Map health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false
});

// Map MCP endpoint
app.MapMcp();

// Build the index on startup
var indexer = app.Services.GetRequiredService<IComponentIndexer>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Building MudBlazor component index...");
    await indexer.BuildIndexAsync();
    logger.LogInformation("Index built successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to build initial index. Server will start without component data.");
}

await app.RunAsync();

/// <summary>
/// Health check for the component indexer.
/// </summary>
public class IndexerHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IComponentIndexer _indexer;

    public IndexerHealthCheck(IComponentIndexer indexer)
    {
        _indexer = indexer;
    }

    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_indexer.IsIndexed)
        {
            return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(
                $"Index built at {_indexer.LastIndexed}"));
        }

        return Task.FromResult(Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded(
            "Index not yet built"));
    }
}
