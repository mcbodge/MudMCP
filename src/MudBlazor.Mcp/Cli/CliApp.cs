// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Cli;

/// <summary>
/// The command-line front end: routing (REQ-001), argument parsing, global options (REQ-003),
/// exit-code mapping (REQ-004), and the command table (REQ-005). Additional commands are
/// registered by later tasks.
/// </summary>
internal static class CliApp
{
    /// <summary>The output format selected by <c>--format</c>.</summary>
    internal enum CliFormat
    {
        Compact,
        Markdown,
        Json,
    }

    /// <summary>
    /// The resolved context passed to a command handler. <see cref="Version"/> is empty when the
    /// version is optional and absent; <see cref="Store"/> points at the version's <c>store</c> folder.
    /// </summary>
    internal sealed record CliContext(string Version, string DataPath, CliFormat Format, IndexStore Store, TextWriter Out);

    /// <summary>A single row of the command table (REQ-005).</summary>
    private sealed record CliCommand(
        string Name,
        string Usage,
        string Summary,
        IReadOnlySet<string> ValueOptions,
        IReadOnlySet<string> FlagOptions,
        int MinPositionals,
        int MaxPositionals,
        bool RequiresVersion,
        bool RequiresStore,
        Func<CliContext, CliArgs, CancellationToken, Task<int>> Handler);

    private static readonly IReadOnlySet<string> NoOptions = new HashSet<string>(StringComparer.Ordinal);

    private static readonly IReadOnlyList<CliCommand> CommandTable = BuildCommandTable();

    private static readonly IReadOnlyDictionary<string, CliCommand> CommandsByName =
        CommandTable.ToDictionary(command => command.Name, StringComparer.Ordinal);

    /// <summary>Gets the registered command names in registration order.</summary>
    internal static IReadOnlyList<string> CommandNames { get; } = CommandTable.Select(command => command.Name).ToArray();

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="args"/> should run as a CLI command
    /// rather than the MCP server (REQ-001).
    /// </summary>
    internal static bool IsCliInvocation(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        var first = args[0];
        if (first is "--help" or "-h")
        {
            return true;
        }

        if (first.StartsWith('-') || first.StartsWith('/'))
        {
            return false;
        }

        if (first.Contains('='))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Entry point for a CLI invocation: wires real console streams, UTF-8 (no BOM) buffered
    /// stdout, and Ctrl+C cancellation, then delegates to the testable core overload.
    /// </summary>
    internal static async Task<int> RunAsync(string[] args)
    {
        await using var stdout = new StreamWriter(
            Console.OpenStandardOutput(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = false,
        };

        using var cts = new CancellationTokenSource();
        ConsoleCancelEventHandler onCancel = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };
        Console.CancelKeyPress += onCancel;
        try
        {
            return await RunAsync(args, stdout, Console.Error, Environment.GetEnvironmentVariable, cts.Token).ConfigureAwait(false);
        }
        finally
        {
            Console.CancelKeyPress -= onCancel;
        }
    }

    /// <summary>
    /// Runs a CLI command, writing command output to <paramref name="stdout"/> and diagnostics to
    /// <paramref name="stderr"/>, and returns the process exit code (REQ-004).
    /// </summary>
    internal static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        Func<string, string?> getEnvironmentVariable,
        CancellationToken cancellationToken)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        string? version = null;

        async Task<int> FinishSuccessAsync(int exit)
        {
            await stdout.FlushAsync(cancellationToken).ConfigureAwait(false);
            await EmitTimingAsync().ConfigureAwait(false);
            return exit;
        }

        async Task<int> FailAsync(int exit, string errorMessage, string? hint)
        {
            await stdout.FlushAsync(CancellationToken.None).ConfigureAwait(false);
            await stderr.WriteLineAsync($"error: {errorMessage}".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            if (hint is not null)
            {
                await stderr.WriteLineAsync($"hint: {hint}".AsMemory(), CancellationToken.None).ConfigureAwait(false);
            }

            await EmitTimingAsync().ConfigureAwait(false);
            return exit;
        }

        async Task EmitTimingAsync()
        {
            if (getEnvironmentVariable("MUDMCP_TIMING") == "1")
            {
                await WriteTimingAsync(stderr, startTimestamp).ConfigureAwait(false);
            }
        }

        try
        {
            CliCommand command;
            string[] rest;
            if (args.Length == 0 || args[0] is "--help" or "-h")
            {
                command = CommandsByName["help"];
                rest = [];
            }
            else
            {
                var name = args[0];
                if (!CommandsByName.TryGetValue(name, out var found))
                {
                    throw new CliException(CliExitCodes.Usage, $"unknown command '{name}'", "mudmcp help");
                }

                command = found;
                rest = args.Skip(1).ToArray();
            }

            if (rest.Contains("--help") || rest.Contains("-h"))
            {
                await WriteCommandHelpAsync(command, stdout, cancellationToken).ConfigureAwait(false);
                return await FinishSuccessAsync(CliExitCodes.Success).ConfigureAwait(false);
            }

            var valueOptions = new HashSet<string>(command.ValueOptions, StringComparer.Ordinal)
            {
                "--version",
                "--data-path",
                "--format",
            };
            var parsed = CliArgs.Parse(rest, valueOptions, command.FlagOptions);

            if (parsed.Positionals.Count < command.MinPositionals || parsed.Positionals.Count > command.MaxPositionals)
            {
                throw new CliException(CliExitCodes.Usage, $"usage: mudmcp {command.Usage}");
            }

            var fmt = parsed.GetValue("--format") switch
            {
                null or "compact" => CliFormat.Compact,
                "markdown" => CliFormat.Markdown,
                "json" => CliFormat.Json,
                var other => throw new CliException(CliExitCodes.Usage, $"invalid format '{other}'"),
            };

            var dataPath = ResolveDataPath(parsed, getEnvironmentVariable);

            version = (parsed.GetValue("--version") ?? getEnvironmentVariable("MUDBLAZOR_VERSION"))?.Trim();
            if (command.RequiresVersion && string.IsNullOrEmpty(version))
            {
                var ready = CliCommands.ReadyVersions(dataPath);
                var hint = ready.Count > 0
                    ? "indexed versions: " + string.Join(", ", ready)
                    : "read Version from <PackageReference Include=\"MudBlazor\" ...> in your .csproj or Directory.Packages.props";
                throw new CliException(
                    CliExitCodes.Usage,
                    "MudBlazor version required: pass --version X.Y.Z or set MUDBLAZOR_VERSION",
                    hint);
            }

            if (!string.IsNullOrEmpty(version) && !VersionArgument.IsValid(version))
            {
                throw new CliException(CliExitCodes.Usage, $"invalid version '{version}'");
            }

            var store = new IndexStore(new VersionContext(version ?? string.Empty, dataPath).StorePath);
            var context = new CliContext(version ?? string.Empty, dataPath, fmt, store, stdout);

            if (command.RequiresStore && !store.IsCurrent(context.Version))
            {
                throw new CliException(
                    CliExitCodes.NotIndexed,
                    $"MudBlazor v{context.Version} is not indexed in {dataPath}",
                    $"mudmcp index --version {context.Version}");
            }

            var exit = await command.Handler(context, parsed, cancellationToken).ConfigureAwait(false);
            return await FinishSuccessAsync(exit).ConfigureAwait(false);
        }
        catch (CliException ex)
        {
            return await FailAsync(ex.ExitCode, ex.Message, ex.Hint).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return await FailAsync(CliExitCodes.Cancelled, "cancelled", hint: null).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return await FailAsync(
                CliExitCodes.Error,
                $"cannot read the index store: {ex.Message}",
                $"mudmcp index --version {version ?? string.Empty}").ConfigureAwait(false);
        }
    }

    private static IReadOnlyList<CliCommand> BuildCommandTable()
    {
        // Rows are appended here by later tasks; registration order defines help listing order.
        return
        [
            new CliCommand(
                Name: "help",
                Usage: "help [command]",
                Summary: "Show help for the CLI or a command",
                ValueOptions: NoOptions,
                FlagOptions: NoOptions,
                MinPositionals: 0,
                MaxPositionals: 1,
                RequiresVersion: false,
                RequiresStore: false,
                Handler: HelpAsync),
            new CliCommand(
                Name: "index-status",
                Usage: "index-status",
                Summary: "Show cached index status",
                ValueOptions: NoOptions,
                FlagOptions: NoOptions,
                MinPositionals: 0,
                MaxPositionals: 0,
                RequiresVersion: false,
                RequiresStore: false,
                Handler: CliCommands.IndexStatusAsync),
            new CliCommand(
                Name: "index",
                Usage: "index",
                Summary: "Build or rebuild the cached index for a version",
                ValueOptions: NoOptions,
                FlagOptions: NoOptions,
                MinPositionals: 0,
                MaxPositionals: 0,
                RequiresVersion: true,
                RequiresStore: false,
                Handler: CliCommands.IndexAsync),
        ];
    }

    private static string ResolveDataPath(CliArgs parsed, Func<string, string?> getEnvironmentVariable)
    {
        var dataPath = parsed.GetValue("--data-path");

        if (string.IsNullOrEmpty(dataPath))
        {
            dataPath = getEnvironmentVariable("MudBlazor__Repository__DataPath");
        }

        if (string.IsNullOrEmpty(dataPath))
        {
            var home = (OperatingSystem.IsWindows()
                ? getEnvironmentVariable("USERPROFILE")
                : getEnvironmentVariable("HOME"))
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            dataPath = Path.Combine(home, ".mudmcp");
        }

        return Path.GetFullPath(dataPath);
    }

    private static async Task<int> HelpAsync(CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        if (args.Positionals.Count == 0)
        {
            await WriteFullHelpAsync(context.Out, cancellationToken).ConfigureAwait(false);
            return CliExitCodes.Success;
        }

        var name = args.Positionals[0];
        if (!CommandsByName.TryGetValue(name, out var command))
        {
            throw new CliException(CliExitCodes.Usage, $"unknown command '{name}'");
        }

        await WriteCommandHelpAsync(command, context.Out, cancellationToken).ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    private static async Task WriteFullHelpAsync(TextWriter writer, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine("usage: mudmcp <command> [arguments] [options]");
        builder.AppendLine();
        builder.AppendLine("commands:");
        foreach (var name in CommandNames)
        {
            var command = CommandsByName[name];
            builder.Append("  ").Append(command.Usage).Append("  ").AppendLine(command.Summary);
        }

        builder.AppendLine();
        builder.AppendLine("global options:");
        builder.AppendLine("  --version X.Y.Z");
        builder.AppendLine("  --data-path <dir>");
        builder.AppendLine("  --format compact|markdown|json");
        builder.AppendLine();
        builder.AppendLine("formats: compact (default), markdown, json");
        builder.AppendLine();
        builder.AppendLine("exit codes: 0 success, 1 error, 2 usage, 3 not indexed, 4 not found, 130 cancelled");
        builder.AppendLine();
        builder.AppendLine("MCP server: mudmcp --stdio or mudmcp --http, with --version X.Y.Z or MUDBLAZOR_VERSION");

        await writer.WriteAsync(builder.ToString().AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteCommandHelpAsync(CliCommand command, TextWriter writer, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.Append("usage: mudmcp ").AppendLine(command.Usage);
        builder.AppendLine(command.Summary);

        if (command.ValueOptions.Count > 0 || command.FlagOptions.Count > 0)
        {
            builder.AppendLine("options:");
            foreach (var option in command.ValueOptions.OrderBy(option => option, StringComparer.Ordinal))
            {
                builder.Append("  ").Append(option).AppendLine(" <value>");
            }

            foreach (var option in command.FlagOptions.OrderBy(option => option, StringComparer.Ordinal))
            {
                builder.Append("  ").AppendLine(option);
            }
        }

        await writer.WriteAsync(builder.ToString().AsMemory(), cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteTimingAsync(TextWriter stderr, long startTimestamp)
    {
        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        using var process = Process.GetCurrentProcess();
        var processMs = (DateTime.Now - process.StartTime).TotalMilliseconds;

        await stderr.WriteLineAsync(
            $"elapsed-ms={elapsedMs.ToString("F1", CultureInfo.InvariantCulture)}".AsMemory(),
            CancellationToken.None).ConfigureAwait(false);
        await stderr.WriteLineAsync(
            $"process-ms={processMs.ToString("F0", CultureInfo.InvariantCulture)}".AsMemory(),
            CancellationToken.None).ConfigureAwait(false);
    }
}
