// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Hosting;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Cli;

/// <summary>
/// Handlers for the CLI command surface (REQ-005). Additional handlers are added by later tasks.
/// </summary>
internal static class CliCommands
{
    private const string ReadyState = "ready";
    private const string StaleState = "stale";
    private const string NotIndexedState = "not-indexed";

    /// <summary>
    /// Implements <c>index-status</c> (REQ-019): reports the cached index state for one version
    /// (when <see cref="CliApp.CliContext.Version"/> is set) or for every <c>v*</c> folder under the
    /// data path. Compact and markdown output lead with a <c>data-path</c> line; JSON output is the
    /// bare status payload. A requested version that is not ready exits 3 with the <c>index</c> hint.
    /// </summary>
    internal static async Task<int> IndexStatusAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var dataPath = context.DataPath;
        var json = context.Format == CliApp.CliFormat.Json;

        if (!json)
        {
            await context.Out.WriteLineAsync($"data-path {dataPath}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(context.Version))
        {
            var status = DeriveStatus(context.Version, dataPath);

            if (json)
            {
                var payload = JsonSerializer.Serialize(status, CliJsonContext.Default.IndexStatus);
                await context.Out.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await context.Out.WriteLineAsync(CompactFormatter.IndexStatusLine(status).AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            if (string.Equals(status.State, ReadyState, StringComparison.Ordinal))
            {
                return CliExitCodes.Success;
            }

            throw new CliException(
                CliExitCodes.NotIndexed,
                $"MudBlazor v{context.Version} is not indexed in {dataPath}",
                $"mudmcp index --version {context.Version}");
        }

        var statuses = EnumerateStatuses(dataPath);

        if (json)
        {
            var payload = JsonSerializer.Serialize(statuses, CliJsonContext.Default.ListIndexStatus);
            await context.Out.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (var status in statuses)
            {
                await context.Out.WriteLineAsync(CompactFormatter.IndexStatusLine(status).AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        return CliExitCodes.Success;
    }

    /// <summary>
    /// Implements <c>index</c> (REQ-018, REQ-026): rebuilds the on-disk store for the requested
    /// version. The stale store is removed first, then the index is (re)built — loading a valid
    /// cached <c>index.json</c> when present so no repository clone is required — and the resulting
    /// <see cref="IndexStatus"/> is printed. A build failure exits 1; a build that leaves no current
    /// store also exits 1.
    /// </summary>
    internal static async Task<int> IndexAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var versionContext = new VersionContext(context.Version, context.DataPath);
        if (Directory.Exists(versionContext.StorePath))
        {
            Directory.Delete(versionContext.StorePath, recursive: true);
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MudBlazor:Repository:DataPath"] = context.DataPath,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            McpServerHost.ConfigureLogging(logging);
            logging.SetMinimumLevel(LogLevel.Information);
        });
        McpServerHost.RegisterCoreServices(services, configuration, context.Version);

        await using var provider = services.BuildServiceProvider();

        try
        {
            var indexer = provider.GetRequiredService<IComponentIndexer>();
            await indexer.BuildIndexAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or LibGit2Sharp.LibGit2SharpException or IOException or UnauthorizedAccessException)
        {
            throw new CliException(CliExitCodes.Error, "index build failed: " + ex.Message);
        }

        var store = new IndexStore(versionContext.StorePath);
        var status = DeriveStatus(context.Version, context.DataPath);

        if (context.Format == CliApp.CliFormat.Json)
        {
            var payload = JsonSerializer.Serialize(status, CliJsonContext.Default.IndexStatus);
            await context.Out.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await context.Out.WriteLineAsync(CompactFormatter.IndexStatusLine(status).AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return store.IsCurrent(context.Version)
            ? CliExitCodes.Success
            : throw new CliException(CliExitCodes.Error, "index built but the CLI store could not be written to " + versionContext.StorePath);
    }

    private static List<IndexStatus> EnumerateStatuses(string dataPath)
    {
        var statuses = new List<IndexStatus>();
        if (!Directory.Exists(dataPath))
        {
            return statuses;
        }

        var versionFolders = Directory.EnumerateDirectories(dataPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith('v'))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var folder in versionFolders)
        {
            statuses.Add(DeriveStatus(folder![1..], dataPath));
        }

        return statuses;
    }

    private static IndexStatus DeriveStatus(string version, string dataPath)
    {
        var versionContext = new VersionContext(version, dataPath);
        var store = new IndexStore(versionContext.StorePath);
        var manifest = store.TryReadManifest();
        var hasIndexJson = File.Exists(versionContext.IndexPath);

        var state = store.IsCurrent(version)
            ? ReadyState
            : manifest is not null ? StaleState : NotIndexedState;

        return new IndexStatus(
            Version: version,
            State: state,
            ComponentCount: manifest?.ComponentCount ?? 0,
            EnumCount: manifest?.EnumCount ?? 0,
            BuiltAtUtc: manifest?.BuiltAtUtc,
            HasIndexJson: hasIndexJson);
    }

    /// <summary>
    /// Returns the versions under <paramref name="dataPath"/> that have a current (ready) CLI store.
    /// The <c>v*</c> folders are enumerated in ordinal order and the leading <c>v</c> is stripped;
    /// only versions whose store <see cref="IndexStore.IsCurrent"/> check passes are returned. The
    /// list is empty when the data path does not exist.
    /// </summary>
    internal static IReadOnlyList<string> ReadyVersions(string dataPath)
    {
        var ready = new List<string>();
        if (!Directory.Exists(dataPath))
        {
            return ready;
        }

        var versionFolders = Directory.EnumerateDirectories(dataPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith('v'))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var folder in versionFolders)
        {
            var version = folder![1..];
            if (new IndexStore(new VersionContext(version, dataPath).StorePath).IsCurrent(version))
            {
                ready.Add(version);
            }
        }

        return ready;
    }
}
