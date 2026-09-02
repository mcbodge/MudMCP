// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Services.Parsing;

// Check for stdio transport mode
var useStdio = args.Contains("--stdio");

// Parse required --version argument (supports both "--version X.Y.Z" and "--version=X.Y.Z")
string? mudBlazorVersion = null;
var versionIndex = Array.IndexOf(args, "--version");
if (versionIndex >= 0 && versionIndex + 1 < args.Length)
{
    // Space-separated form: --version X.Y.Z
    var candidate = args[versionIndex + 1];
    if (!string.IsNullOrEmpty(candidate) && candidate[0] != '-')
    {
        mudBlazorVersion = candidate;
    }
}
else
{
    // Equals form: --version=X.Y.Z
    var equalsArg = args.FirstOrDefault(a => a.StartsWith("--version=", StringComparison.OrdinalIgnoreCase));
    if (equalsArg is not null)
    {
        mudBlazorVersion = equalsArg["--version=".Length..];
    }
}

// Normalize CLI argument: trim and treat empty/whitespace as missing
if (!string.IsNullOrWhiteSpace(mudBlazorVersion))
{
    mudBlazorVersion = mudBlazorVersion.Trim();
}
else
{
    mudBlazorVersion = null;
}

// Fallback to environment variable (used by IIS hosting via web.config environmentVariables)
if (string.IsNullOrWhiteSpace(mudBlazorVersion))
{
    var envVersion = Environment.GetEnvironmentVariable("MUDBLAZOR_VERSION");
    if (!string.IsNullOrWhiteSpace(envVersion))
    {
        mudBlazorVersion = envVersion.Trim();
    }
    else
    {
        // Treat null, empty, or whitespace-only environment values as missing
        mudBlazorVersion = null;
    }
}

if (string.IsNullOrWhiteSpace(mudBlazorVersion))
{
    Console.Error.WriteLine("Error: --version argument or MUDBLAZOR_VERSION environment variable is required.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("The MudBlazor MCP server requires a specific MudBlazor version to serve documentation for.");
    Console.Error.WriteLine("Find your version in your project's .csproj file: <PackageReference Include=\"MudBlazor\" Version=\"X.Y.Z\" />");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Option 1: Add --version to your .mcp.json configuration:");
    Console.Error.WriteLine();
    Console.Error.WriteLine("  {");
    Console.Error.WriteLine("    \"mcpServers\": {");
    Console.Error.WriteLine("      \"mudblazor\": {");
    Console.Error.WriteLine("        \"command\": \"dotnet\",");
    Console.Error.WriteLine("        \"args\": [");
    Console.Error.WriteLine("          \"run\", \"--project\", \"<path-to-MudMCP>/src/MudBlazor.Mcp/MudBlazor.Mcp.csproj\",");
    Console.Error.WriteLine("          \"--\", \"--stdio\", \"--version\", \"9.0.0\"");
    Console.Error.WriteLine("        ]");
    Console.Error.WriteLine("      }");
    Console.Error.WriteLine("    }");
    Console.Error.WriteLine("  }");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Option 2: Set the MUDBLAZOR_VERSION environment variable (e.g., in IIS web.config).");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Replace 9.0.0 with your project's MudBlazor version.");
    return 1;
}

if (!System.Text.RegularExpressions.Regex.IsMatch(mudBlazorVersion, @"^\d+\.\d+\.\d+(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$"))
{
    Console.Error.WriteLine($"Error: '{mudBlazorVersion}' is not a valid version. Expected format: X.Y.Z or X.Y.Z-prerelease (e.g., 9.0.0 or 9.0.0-preview.1)");
    return 1;
}

// Strip custom arguments so they don't leak into the .NET host builder configuration.
var filteredArgs = StripCustomArgs(args);

if (useStdio)
{
    // Stdio mode: plain console host — no Kestrel, no health endpoints.
    // All logs must go to stderr so stdout remains clean for MCP protocol frames.
    var builder = Host.CreateApplicationBuilder(filteredArgs);

    ConfigureLogging(builder.Logging);

    RegisterCoreServices(builder.Services, builder.Configuration, mudBlazorVersion);

    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = $"MudBlazor Documentation Server (v{mudBlazorVersion})", Version = "1.0.0" };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

    var host = builder.Build();

    await host.RunAsync();

    return 0;
}
else
{
    // HTTP mode: full ASP.NET Core web host with health checks and streamable HTTP transport.
    var builder = WebApplication.CreateBuilder(filteredArgs);

    ConfigureLogging(builder.Logging);

    RegisterCoreServices(builder.Services, builder.Configuration, mudBlazorVersion);

    builder.Services.AddHealthChecks()
        .AddCheck<IndexerHealthCheck>("indexer", tags: ["ready"]);

    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = $"MudBlazor Documentation Server (v{mudBlazorVersion})", Version = "1.0.0" };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

    var app = builder.Build();

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

    app.MapMcp("/mcp");

    await app.RunAsync();

    return 0;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

static string[] StripCustomArgs(string[] args)
{
    var filtered = new List<string>(args.Length);
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--stdio")
            continue;

        if (args[i] == "--version")
        {
            // Always strip the --version flag; if a non-option value follows, skip it as well.
            if (i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal))
            {
                i++; // skip the value token as well
            }
            continue;
        }

        if (args[i].StartsWith("--version=", StringComparison.OrdinalIgnoreCase))
            continue;

        filtered.Add(args[i]);
    }

    return [.. filtered];
}

static void ConfigureLogging(ILoggingBuilder logging)
{
    // All logs go to stderr so stdout stays clean for MCP protocol frames in stdio mode.
    logging.ClearProviders();
    logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
}

static void RegisterCoreServices(IServiceCollection services, IConfiguration configuration, string version)
{
    services.Configure<MudBlazorOptions>(configuration.GetSection(MudBlazorOptions.SectionName));
    services.Configure<RepositoryOptions>(configuration.GetSection($"{MudBlazorOptions.SectionName}:Repository"));
    services.Configure<ParsingOptions>(configuration.GetSection($"{MudBlazorOptions.SectionName}:Parsing"));

    // Read config to derive shared data path
    var repoOptions = configuration.GetSection($"{MudBlazorOptions.SectionName}:Repository").Get<RepositoryOptions>() ?? new RepositoryOptions();
    services.AddSingleton(new VersionContext(version, repoOptions.DataPath));
    services.AddSingleton<IVersionCacheManager>(sp =>
        new VersionCacheManager(
            repoOptions.DataPath,
            repoOptions.MaxCachedVersions,
            logger: sp.GetRequiredService<ILogger<VersionCacheManager>>()));

    services.AddSingleton<IGitRepositoryService, GitRepositoryService>();
    services.AddSingleton<IComponentIndexer, ComponentIndexer>();

    // Build the index in the background so the MCP transport can serve the initialize
    // handshake immediately; tools report "not ready" until the first build completes.
    services.AddHostedService<IndexInitializationService>();

    services.AddSingleton<SemanticComponentParser>();
    services.AddSingleton<XmlDocParser>();
    services.AddSingleton<RazorDocParser>();
    services.AddSingleton<ExampleExtractor>();
    services.AddSingleton<MenuCategoryParser>();
}

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

// ---------------------------------------------------------------------------
// Health check implementation
// ---------------------------------------------------------------------------

/// <summary>Health check for the component indexer with detailed status.</summary>
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
