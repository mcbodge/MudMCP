// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Services.Parsing;

namespace MudBlazor.Mcp.Hosting;

internal static class McpServerHost
{
    internal static async Task<int> RunAsync(string[] args)
    {
        // Check for stdio transport mode
        var useStdio = args.Contains("--stdio");
        var useHttp = args.Contains("--http");

        if (useStdio && useHttp)
        {
            Console.Error.WriteLine("Error: --stdio and --http cannot be combined; choose one transport.");
            return 1;
        }

        // Parse required --version argument (supports both "--version X.Y.Z" and "--version=X.Y.Z")
        var mudBlazorVersion = VersionArgument.Resolve(args, Environment.GetEnvironmentVariable);

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

        if (!VersionArgument.IsValid(mudBlazorVersion))
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
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    internal static string[] StripCustomArgs(string[] args)
    {
        var filtered = new List<string>(args.Length);
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] == "--stdio")
                continue;

            if (args[i] == "--http")
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

    internal static void ConfigureLogging(ILoggingBuilder logging)
    {
        // All logs go to stderr so stdout stays clean for MCP protocol frames in stdio mode.
        logging.ClearProviders();
        logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    }

    internal static void RegisterCoreServices(IServiceCollection services, IConfiguration configuration, string version)
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

    internal static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
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
}
