// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using MudDocs.Cli;
using MudDocs.Configuration;
using MudDocs.Models;
using MudDocs.Services;
using MudDocs.Tools;

namespace MudDocs.Tests.Cli;

public class CliExampleCommandsTests
{
    private static readonly IReadOnlyList<ComponentExample> Examples =
    [
        new("Filled Extended", "An expanded demo.", "    <MudButton>Extended</MudButton>  \n", null,
            "Extended.razor", ["Variant", "Color", "Disabled", "Fourth"]),
        new("Filled", null, "    <MudButton>Filled</MudButton>", null, null, []),
        new("Loading State", "Shows asynchronous processing.",
            "\r\n    <MudButton>\t \r\n        Loading\r\n \t\r\n\r\n\r\n    </MudButton>  \r\n\r\n",
            "\n\tprivate void Process()  \n\t{\t\n\t\tRun(); \n \n\n\t}\t\n\n",
            "Loading.razor", ["OnClick"]),
        new("Empty", null, null, null, null, []),
        new("Last", null, "<MudButton>Last</MudButton>", "private bool _last;", "Last.razor", ["Final"]),
    ];

    private const string LoadingBody =
        "=== Loading State\n<MudButton>\n    Loading\n\n</MudButton>\n" +
        "--- code\nprivate void Process()\n{\n\tRun();\n\n}\n";

    [Theory]
    [InlineData("compact")]
    [InlineData("markdown")]
    [InlineData("json")]
    public async Task Examples_DefaultLimitIsThree(string format)
    {
        using var store = await CreateStoreAsync();

        var (exit, output, error) = await RunAsync(store, ["examples", "MudButton", "--format", format]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        if (format == "json")
        {
            using var json = JsonDocument.Parse(output);
            Assert.Equal(3, json.RootElement.GetArrayLength());
            Assert.Equal("Loading State", json.RootElement[2].GetProperty("name").GetString());
            Assert.Equal(Examples[2].RazorMarkup, json.RootElement[2].GetProperty("razorMarkup").GetString());
            Assert.Equal(Examples[2].CSharpCode, json.RootElement[2].GetProperty("cSharpCode").GetString());
            Assert.False(json.RootElement[1].TryGetProperty("description", out _));
            Assert.DoesNotContain('\n', output.TrimEnd('\n'));
        }
        else
        {
            Assert.Contains("Loading State", output);
            Assert.DoesNotContain(format == "compact" ? "=== Empty" : "## Empty", output);
            Assert.DoesNotContain(format == "compact" ? "=== Last" : "## Last", output);
            if (format == "compact")
                Assert.StartsWith("MudButton v9.0.0 examples 3/5\n", output);
        }
    }

    [Fact]
    public async Task Examples_Compact_DedentsAndSeparatesCode()
    {
        using var store = await CreateStoreAsync();

        var (exit, output, error) = await RunAsync(store, ["examples", "Button", "--filter", "loading"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 examples 1/5\n" + LoadingBody, output);
        Assert.DoesNotContain("```", output);
        Assert.DoesNotContain("Shows asynchronous", output);
        Assert.DoesNotContain("Loading.razor", output);
    }

    [Fact]
    public async Task Examples_NamesOnly_ShowsFirstThreeFeatures()
    {
        using var store = await CreateStoreAsync();

        var (exit, output, error) = await RunAsync(store, ["examples", "Button", "--names-only", "--limit", "1"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 examples 5\n" +
            "Filled Extended [Variant, Color, Disabled]\nFilled\nLoading State [OnClick]\nEmpty\nLast [Final]\n", output);
    }

    [Theory]
    [InlineData("json")]
    [InlineData("markdown")]
    public async Task Examples_NamesOnly_AllFilteredExamplesWithoutLimit(string format)
    {
        using var store = await CreateStoreAsync();

        var (exit, output, error) = await RunAsync(store,
            ["examples", "Button", "--names-only", "--limit", "1", "--format", format]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        if (format == "markdown")
        {
            Assert.Equal(ComponentExampleTools.RenderExampleList("MudButton", Examples, "9.0.0"), output);
        }
        else
        {
            using var json = JsonDocument.Parse(output);
            Assert.Equal(5, json.RootElement.GetArrayLength());
            Assert.Equal(4, json.RootElement[0].GetProperty("features").GetArrayLength());
            Assert.All(json.RootElement.EnumerateArray(), example =>
            {
                Assert.False(example.TryGetProperty("razorMarkup", out _));
                Assert.False(example.TryGetProperty("cSharpCode", out _));
            });
            Assert.Equal("Loading.razor", json.RootElement[2].GetProperty("sourceFile").GetString());
            Assert.DoesNotContain('\n', output.TrimEnd('\n'));
        }
    }

    [Theory]
    [InlineData("fIlLeD")]
    [InlineData("aSyNcHrOnOuS")]
    [InlineData("oNcLiCk")]
    [InlineData(" ")]
    [InlineData("*Filled*")]
    [InlineData("missing")]
    public async Task Examples_Filter_UsesMcpSemantics(string filter)
    {
        using var store = await CreateStoreAsync();
        var expected = ComponentExampleTools.FilterExamples(Examples, filter);

        var (exit, output, error) = await RunAsync(store,
            ["examples", "Button", "--filter", filter, "--names-only", "--format", "json"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        using var json = JsonDocument.Parse(output);
        Assert.Equal(expected.Select(e => e.Name),
            json.RootElement.EnumerateArray().Select(e => e.GetProperty("name").GetString()));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("21")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public async Task Examples_LimitOutOfRange_ReturnsTwo(string limit)
    {
        using var store = await CreateStoreAsync();

        var (exit, output, error) = await RunAsync(store, ["examples", "Button", "--limit=" + limit]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: --limit", error);
    }

    [Fact]
    public async Task Examples_NamesOnly_StillValidatesLimit()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["examples", "Button", "--names-only", "--limit", "21"]);
        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: --limit", error);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(20)]
    public async Task Examples_LimitBoundaries_ApplyToNormalJson(int limit)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store,
            ["examples", "Button", "--limit", limit.ToString(), "--format", "json"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        using var json = JsonDocument.Parse(output);
        Assert.Equal(Math.Min(limit, Examples.Count), json.RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("Filled", 1)]
    [InlineData("OnClick", 20)]
    [InlineData("missing", 3)]
    public async Task Examples_Markdown_EqualsRenderExamples(string? filter, int limit)
    {
        using var store = await CreateStoreAsync();
        List<string> args = ["examples", "Button", "--format", "markdown", "--limit", limit.ToString()];
        if (filter is not null)
            args.AddRange(["--filter", filter]);

        var (exit, output, error) = await RunAsync(store, args.ToArray());

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(ComponentExampleTools.RenderExamples("MudButton",
            ComponentExampleTools.FilterExamples(Examples, filter), limit, "9.0.0"), output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Examples_NamesOnly_Markdown_FiltersBeforeRendering(bool namesOnly)
    {
        using var store = await CreateStoreAsync();
        List<string> args = ["examples", "Button", "--format", "markdown", "--filter", "Filled"];
        if (namesOnly)
            args.Add("--names-only");
        var (exit, output, error) = await RunAsync(store, args.ToArray());
        var filtered = ComponentExampleTools.FilterExamples(Examples, "Filled");
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(namesOnly
            ? ComponentExampleTools.RenderExampleList("MudButton", filtered, "9.0.0")
            : ComponentExampleTools.RenderExamples("MudButton", filtered, 3, "9.0.0"), output);
    }

    [Theory]
    [InlineData(false, "compact", "MudButton v9.0.0 examples 0/5\n")]
    [InlineData(true, "compact", "MudButton v9.0.0 examples 0\n")]
    [InlineData(false, "json", "[]\n")]
    [InlineData(true, "json", "[]\n")]
    public async Task Examples_ZeroMatches_Succeeds(bool namesOnly, string format, string expected)
    {
        using var store = await CreateStoreAsync();
        List<string> args = ["examples", "Button", "--filter", "missing", "--format", format];
        if (namesOnly)
            args.Add("--names-only");
        var (exit, output, error) = await RunAsync(store, args.ToArray());
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData("compact", "MudIconButton v9.0.0 examples 0/0\n")]
    [InlineData("json", "[]\n")]
    public async Task Examples_NoStoredExamples_Succeeds(string format, string expected)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["examples", "IconButton", "--format", format]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData("fIlLeD", "Filled")]
    [InlineData("extended", "Filled Extended")]
    [InlineData("fill", "Filled Extended")]
    public async Task Example_ExactThenContains(string query, string expected)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["example", "Button", query, "--format", "json"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        using var json = JsonDocument.Parse(output);
        Assert.Equal(expected, json.RootElement.GetProperty("name").GetString());
    }

    [Theory]
    [InlineData("compact")]
    [InlineData("markdown")]
    [InlineData("json")]
    public async Task Example_JoinedPositionals_RenderSelectedExample(string format)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store,
            ["example", "Button<T>", "Loading", "State", "--format", format]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        if (format == "compact")
            Assert.Equal("MudButton v9.0.0 example Loading State\n" + LoadingBody, output);
        else if (format == "markdown")
            Assert.Equal(ComponentExampleTools.RenderExample("MudButton", Examples[2], "9.0.0"), output);
        else
        {
            using var json = JsonDocument.Parse(output);
            Assert.Equal("Loading State", json.RootElement.GetProperty("name").GetString());
        }
    }

    [Fact]
    public async Task Example_Unknown_ReturnsFourListingAvailable()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["example", "Button", "No", "Such"]);
        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.Equal("error: example 'No Such' not found for MudButton\n" +
            "hint: available: Filled Extended, Filled, Loading State, Empty, Last\n", error);
    }

    [Fact]
    public async Task Example_Json_EmitsExample()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store,
            ["example", "Button", "Loading State", "--format", "json"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.DoesNotContain('\n', output.TrimEnd('\n'));
        using var json = JsonDocument.Parse(output);
        var example = json.RootElement;
        Assert.Equal("Loading State", example.GetProperty("name").GetString());
        Assert.Equal(Examples[2].Description, example.GetProperty("description").GetString());
        Assert.Equal(Examples[2].RazorMarkup, example.GetProperty("razorMarkup").GetString());
        Assert.Equal(Examples[2].CSharpCode, example.GetProperty("cSharpCode").GetString());
        Assert.Equal("Loading.razor", example.GetProperty("sourceFile").GetString());
        Assert.Equal("OnClick", example.GetProperty("features")[0].GetString());
    }

    [Theory]
    [InlineData("  \n\t\r\n", null, "")]
    [InlineData(null, "    Run();  \n", "--- code\nRun();\n")]
    [InlineData("  first\n    second\n   \n \n  third\n", null, "first\n  second\n\nthird\n")]
    [InlineData(" \tfirst\n \t  second\n", null, "first\n  second\n")]
    public async Task Example_Compact_CleansWhitespaceAndOptionalCode(string? razor, string? code, string expectedBody)
    {
        using var store = await CreateStoreAsync(
            [new ComponentExample("Whitespace", null, razor, code, null, [])]);
        var (exit, output, error) = await RunAsync(store, ["example", "Button", "Whitespace"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 example Whitespace\n=== Whitespace\n" + expectedBody, output);
    }

    [Theory]
    [InlineData("examples", "NoSuchComponent")]
    [InlineData("example", "NoSuchComponent", "Filled")]
    public async Task Commands_MissingComponent_ReturnsFour(params string[] args)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, args);
        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: component 'NoSuchComponent' not found", error);
    }

    [Theory]
    [InlineData("examples")]
    [InlineData("example", "Button")]
    [InlineData("examples", "Button", "extra")]
    [InlineData("example", "Button", "Filled", "--limit", "1")]
    public async Task Commands_InvalidArguments_ReturnTwo(params string[] args)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, args);
        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: ", error);
    }

    private static async Task<CliTestStore> CreateStoreAsync(IReadOnlyList<ComponentExample>? examples = null)
    {
        var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var path = new VersionContext("9.0.0", store.DataPath).StorePath;
        var index = new IndexStore(path);
        var components = index.ReadCatalog().Select(component =>
            index.TryReadComponent(component.Name)!).Select(component =>
                component.Name == "MudButton" ? component with { Examples = examples ?? Examples } : component).ToList();
        await IndexStore.WriteAsync(path, index.TryReadManifest()!, components, index.ReadEnums(),
            TestContext.Current.CancellationToken);
        return store;
    }

    private static async Task<(int Exit, string Output, string Error)> RunAsync(CliTestStore store, string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(
            [.. args, "--version", "9.0.0", "--data-path", store.DataPath],
            stdout, stderr, _ => null, TestContext.Current.CancellationToken);
        return (exit, stdout.ToString(), stderr.ToString().Replace("\r\n", "\n"));
    }
}
