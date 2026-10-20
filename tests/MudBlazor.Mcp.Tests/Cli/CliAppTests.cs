// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudBlazor.Mcp.Cli;

namespace MudBlazor.Mcp.Tests.Cli;

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
        Assert.Contains("hint: mudmcp help", stderr.ToString());
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
            ["MudBlazor__Repository__DataPath"] = Path.Combine(Path.GetTempPath(), "mudmcp-other-" + Guid.NewGuid().ToString("N")),
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

        var home = Path.Combine(Path.GetTempPath(), "mudmcp-home-" + Guid.NewGuid().ToString("N"));
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
    public async Task DataPath_DefaultsToDotMudmcpUnderHome()
    {
        var home = Path.Combine(Path.GetTempPath(), "mudmcp-home-" + Guid.NewGuid().ToString("N"));
        using var store = new CliTestStore(Path.Combine(home, ".mudmcp"));
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
        Assert.Contains(".mudmcp", firstLine);
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
        Assert.Contains("mudmcp index --version 9.0.0", stderr.ToString());
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
            ["MUDMCP_TIMING"] = "1",
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
}
