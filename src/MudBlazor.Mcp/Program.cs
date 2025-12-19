// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Services.Parsing;

var builder = WebApplication.CreateBuilder(args);

// Check for stdio transport mode
var useStdio = args.Contains("--stdio");

// Configure logging to stderr for MCP compatibility (required for stdio transport)
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

// Health checks with detailed status
builder.Services.AddHealthChecks()
    .AddCheck<IndexerHealthCheck>("indexer", tags: ["ready"]);

// Add MCP server with configurable transport
if (useStdio)
{
    // Use stdio transport for CLI-based MCP clients
    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "MudBlazor Documentation Server",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();
}
else
{
    // Use HTTP transport for web-based MCP clients
    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "MudBlazor Documentation Server",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();
}

var app = builder.Build();

// Map health check endpoints (only for HTTP transport)
if (!useStdio)
{
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = WriteHealthCheckResponse
    });
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthCheckResponse
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false,
        ResponseWriter = WriteHealthCheckResponse
    });

    // Map MCP endpoint
    app.MapMcp();
}

// Build the index on startup
var indexer = app.Services.GetRequiredService<IComponentIndexer>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("Building MudBlazor component index...");
    await indexer.BuildIndexAsync();
    logger.LogInformation("Index built successfully with {ComponentCount} components",
        (await indexer.GetAllComponentsAsync()).Count);
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to build initial index. Server will start without component data.");
}

await app.RunAsync();

/// <summary>
/// Writes a detailed JSON health check response.
/// </summary>
static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";
    
    var response = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            duration = entry.Value.Duration.TotalMilliseconds,
            data = entry.Value.Data,
            exception = entry.Value.Exception?.Message
        })
    };

    return context.Response.WriteAsJsonAsync(response);
}

/// <summary>
/// Health check for the component indexer with detailed status.
/// </summary>
public class IndexerHealthCheck : IHealthCheck
{
    private readonly IComponentIndexer _indexer;
    private readonly ILogger<IndexerHealthCheck> _logger;

    public IndexerHealthCheck(IComponentIndexer indexer, ILogger<IndexerHealthCheck> logger)
    {
        _indexer = indexer;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_indexer.IsIndexed)
            {
                _logger.LogDebug("Indexer health check: Index not yet built");
                return HealthCheckResult.Degraded(
                    "Component index is not yet built. The server may still be initializing.",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "building",
                        ["componentCount"] = 0,
                        ["isIndexed"] = false
                    });
            }

            var components = await _indexer.GetAllComponentsAsync(cancellationToken);
            var categories = await _indexer.GetCategoriesAsync(cancellationToken);

            if (components.Count == 0)
            {
                _logger.LogWarning("Indexer health check: No components indexed");
                return HealthCheckResult.Degraded(
                    "Component index is empty. There may be a parsing issue.",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "empty",
                        ["componentCount"] = 0,
                        ["categoryCount"] = categories.Count,
                        ["isIndexed"] = true,
                        ["lastIndexed"] = _indexer.LastIndexed?.ToString("O") ?? "never"
                    });
            }

            _logger.LogDebug("Indexer health check passed: {Count} components", components.Count);
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexer health check failed");
            return HealthCheckResult.Unhealthy(
                "Failed to check component index health.",
                ex,
                data: new Dictionary<string, object>
                {
                    ["status"] = "error",
                    ["error"] = ex.Message
                });
        }
    }
}
