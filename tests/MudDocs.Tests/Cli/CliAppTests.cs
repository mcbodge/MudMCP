// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using MudDocs.Cli;

namespace MudDocs.Tests.Cli;

public class CliAppTests
{
    private static Func<string, string?> Env(Dictionary<string, string?> values) =>
        key => values.TryGetValue(key, out var value) ? value : null;

    private static Func<string, string?> EmptyEnv() =>
        Env(new Dictionary<string, string?>(StringComparer.Ordinal));

    [Theory]
    [InlineData(new[] { "search" }, true)]
    [InlineData(new[] { "props" }, true)]
    [InlineData(new[] { "help" }, true)]
    [InlineData(new[] { "--help" }, true)]
    [InlineData(new[] { "-h" }, true)]
    [InlineData(new[] { "serach" }, true)]
    [InlineData(new string[] { }, false)]
    [InlineData(new[] { "--stdio" }, false)]
    [InlineData(new[] { "--http" }, false)]
    [InlineData(new[] { "--version" }, false)]
    [InlineData(new[] { "--urls" }, false)]
    [InlineData(new[] { "urls=http://localhost:9000" }, false)]
    [InlineData(new[] { "/urls" }, false)]
    public void IsCliInvocation_ClassifiesFirstArgument(string[] args, bool expected) =>
        Assert.Equal(expected, CliApp.IsCliInvocation(args));

    [Fact]
    public async Task Help_ListsEveryCommandName()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(["help"], stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        var output = stdout.ToString();
        foreach (var name in CliApp.CommandNames)
        {
            Assert.Contains(name, output);
        }
    }

    [Fact]
    public async Task SubcommandHelpFlag_OutputsConcise3LineHelp()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(["props", "--help"], stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        var lines = stdout.ToString().TrimEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        Assert.True(lines.Length <= 3, $"Expected <= 3 lines for subcommand help, got {lines.Length}");
        Assert.StartsWith("usage: muddocs props", lines[0]);
        Assert.StartsWith("options:", lines[1]);
        Assert.StartsWith("example: muddocs props", lines[2]);
    }

    [Fact]
    public async Task GlobalHelp_OutputsFlatSingleTurnText()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(["--help"], stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        var output = stdout.ToString();
        Assert.Contains("usage: muddocs <command>", output);
        Assert.Contains("global options:", output);
        Assert.Contains("MCP server:", output);
    }

    [Fact]
    public async Task Help_UnknownCommandName_ReturnsTwo()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(["help", "bogus"], stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
    }

    [Fact]
    public async Task UnknownCommand_ReturnsTwoWithHelpHint()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(["bogus"], stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
        Assert.Contains("hint: muddocs help", stderr.ToString());
    }

    [Fact]
    public async Task CommandHelpFlag_PrintsUsageAndReturnsZero()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(["index-status", "--help"], stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Contains("index-status", stdout.ToString());
    }

    [Fact]
    public async Task InvalidFormat_ReturnsTwo()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(["index-status", "--format", "xml"], stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
    }

    [Fact]
    public async Task InvalidVersion_ReturnsTwo()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(["index-status", "--version", "9.0"], stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
    }

    [Fact]
    public async Task DataPath_FlagWinsOverEnvironment()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var env = Env(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["MudBlazor__Repository__DataPath"] = Path.Combine(Path.GetTempPath(), "muddocs-other-" + Guid.NewGuid().ToString("N")),
        });

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(
            ["index-status", "--version", "9.0.0", "--data-path", store.DataPath],
            stdout, stderr, env, TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Contains("9.0.0 ready", stdout.ToString());
    }

    [Fact]
    public async Task DataPath_EnvironmentWinsOverHome()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var home = Path.Combine(Path.GetTempPath(), "muddocs-home-" + Guid.NewGuid().ToString("N"));
        var env = Env(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["MudBlazor__Repository__DataPath"] = store.DataPath,
            ["USERPROFILE"] = home,
            ["HOME"] = home,
        });

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(
            ["index-status", "--version", "9.0.0"],
            stdout, stderr, env, TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        Assert.Contains("9.0.0 ready", stdout.ToString());
    }

    [Fact]
    public async Task DataPath_DefaultsToDotMuddocsUnderHome()
    {
        var home = Path.Combine(Path.GetTempPath(), "muddocs-home-" + Guid.NewGuid().ToString("N"));
        using var store = new CliTestStore(Path.Combine(home, ".muddocs"));
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var env = Env(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["USERPROFILE"] = home,
            ["HOME"] = home,
        });

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(
            ["index-status", "--version", "9.0.0"],
            stdout, stderr, env, TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        var firstLine = stdout.ToString().Split('\n')[0];
        Assert.Contains(".muddocs", firstLine);
    }

    [Fact]
    public async Task IndexStatus_ListsReadyStaleAndNotIndexedVersions()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        await store.WriteStale("8.0.0", TestContext.Current.CancellationToken);
        store.WriteNotIndexed("7.0.0");

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(
            ["index-status", "--data-path", store.DataPath],
            stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        var output = stdout.ToString();
        Assert.Contains("9.0.0 ready", output);
        Assert.Contains("8.0.0 stale", output);
        Assert.Contains("7.0.0 not-indexed", output);
    }

    [Fact]
    public async Task IndexStatus_WithVersionNotIndexed_ReturnsThreeWithIndexHint()
    {
        using var store = new CliTestStore();
        store.WriteNotIndexed("9.0.0");

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(
            ["index-status", "--version", "9.0.0", "--data-path", store.DataPath],
            stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(3, exit);
        Assert.Contains("muddocs index --version 9.0.0", stderr.ToString());
    }

    [Fact]
    public async Task IndexStatus_Json_ReturnsStatusList()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(
            ["index-status", "--format", "json", "--data-path", store.DataPath],
            stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        var output = stdout.ToString().Trim();
        Assert.StartsWith("[", output);
        Assert.Contains("\"ready\"", output);
    }

    [Fact]
    public async Task Timing_WhenEnabled_WritesElapsedAndProcessLinesToStderr()
    {
        using var store = new CliTestStore();

        var env = Env(new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["MUDDOCS_TIMING"] = "1",
        });

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        await CliApp.RunAsync(
            ["index-status", "--data-path", store.DataPath],
            stdout, stderr, env, TestContext.Current.CancellationToken);

        var errorOutput = stderr.ToString();
        Assert.Contains("elapsed-ms=", errorOutput);
        Assert.Contains("process-ms=", errorOutput);
    }

    [Fact]
    public async Task Stdout_ContainsOnlyCommandOutput()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(
            ["index-status", "--version", "9.0.0", "--data-path", store.DataPath],
            stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

        Assert.Equal(0, exit);
        var output = stdout.ToString();
        Assert.DoesNotContain("error:", output);
        Assert.DoesNotContain("hint:", output);
    }

    [Theory]
    [InlineData("help", 0)]
    [InlineData("bogus", 2)]
    public async Task Timing_ExcludesDiagnosticsEnvironmentCallback(string command, int expectedExit)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var timingCalls = 0;
        string? EnvironmentWithSlowTiming(string key)
        {
            if (key != "MUDDOCS_TIMING")
            {
                return null;
            }

            timingCalls++;
            Thread.Sleep(300);
            return "1";
        }

        var stopwatch = Stopwatch.StartNew();
        var exit = await CliApp.RunAsync(
            [command], stdout, stderr, EnvironmentWithSlowTiming, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.Equal(expectedExit, exit);
        Assert.Equal(1, timingCalls);
        var elapsedLine = Assert.Single(stderr.ToString().Split('\n'),
            line => line.StartsWith("elapsed-ms=", StringComparison.Ordinal));
        var reportedElapsed = double.Parse(elapsedLine["elapsed-ms=".Length..], CultureInfo.InvariantCulture);
        Assert.True(stopwatch.Elapsed.TotalMilliseconds - reportedElapsed >= 250,
            "Reported elapsed must exclude the synchronous 300ms diagnostics callback.");
    }
}
