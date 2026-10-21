// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using MudBlazor.Mcp.Cli;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Tools;

namespace MudBlazor.Mcp.Tests.Cli;

public class CliCatalogCommandsTests
{
    [Theory]
    [InlineData("catalog.json", false, "list")]
    [InlineData("catalog.json", true, "list")]
    [InlineData("catalog.json", false, "related", "MudButton")]
    [InlineData("catalog.json", true, "related", "MudButton")]
    [InlineData("enums.json", false, "enum", "Color")]
    [InlineData("enums.json", true, "enum", "Color")]
    public async Task RequiredPayload_MissingOrNull_ReturnsOne(
        string file, bool nullPayload, params string[] arguments)
    {
        using var store = await CreateStoreAsync();
        var path = Path.Combine(new VersionContext("9.0.0", store.DataPath).StorePath, file);
        if (nullPayload)
            await File.WriteAllTextAsync(path, "null", TestContext.Current.CancellationToken);
        else
            File.Delete(path);

        var (exit, output, error) = await RunAsync(store, arguments);

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: ", error);
        Assert.Contains(path, error);
    }

    [Fact]
    public async Task Related_Compact_GroupsParentAndSiblings()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["related", "MudButton"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 related 2\nparent: MudBaseButton\nsiblings: MudIconButton\n", output);
    }

    [Theory]
    [InlineData("Button")]
    [InlineData("mUdBuTtOn")]
    [InlineData(" button<T> ")]
    public async Task Related_ShortName_Resolves(string name)
    {
        using var store = await CreateStoreAsync();
        // Related queries must use the catalog, not the per-component files.
        Directory.Delete(Path.Combine(new VersionContext("9.0.0", store.DataPath).StorePath, "components"), true);
        var (exit, output, error) = await RunAsync(store, ["related", name]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 related 2\nparent: MudBaseButton\nsiblings: MudIconButton\n", output);
    }

    [Fact]
    public async Task Related_ExactName_PrecedesMudPrefix()
    {
        using var store = await CreateStoreAsync(transform: components =>
            [.. components, components[0] with
            {
                Name = "Button", BaseType = null, Category = "Exact", RelatedComponents = [],
            }]);
        var (exit, output, error) = await RunAsync(store, ["related", "button"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("Button v9.0.0 related 0\n", output);
    }

    [Theory]
    [InlineData("all", "MudButton", "MudIconButton,MudBaseButton")]
    [InlineData("parent", "MudButton", "MudIconButton,MudBaseButton")]
    [InlineData("child", "MudBaseButton", "MudButton,MudIconButton")]
    [InlineData("sibling", "MudButton", "MudIconButton")]
    [InlineData("commonly_used_with", "MudButton", "MudIconButton")]
    [InlineData("PaReNt", "MudButton", "MudIconButton,MudBaseButton")]
    public async Task Related_Type_UsesSharedQueryAndJsonSummaries(string type, string name, string names)
    {
        using var store = await CreateStoreAsync();
        var catalog = ReadStore(store).ReadCatalog().ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var related = ComponentQueries.FindRelated(catalog, name, ComponentSearchTools.ParseRelationshipType(type));
        Assert.Equal(names.Split(','), related.Select(c => c.Name));
        var (exit, output, error) = await RunAsync(store,
            ["related", name, "--type", type, "--format", "json"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(JsonSerializer.Serialize<IReadOnlyList<ComponentSummary>>(
            related.Select(ComponentSummary.From).ToList(),
            CliJsonContext.Default.IReadOnlyListComponentSummary) + "\n", output);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("children")]
    [InlineData("commonlyusedwith")]
    [InlineData("")]
    public async Task Related_InvalidType_ReturnsTwo(string type)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["related", "Button", "--type=" + type]);
        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: ", error);
        Assert.Contains("--type", error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("../Button")]
    [InlineData("Mud\\Button")]
    [InlineData("Mud:Button")]
    [InlineData("Büttön")]
    [InlineData("<T>")]
    public async Task Related_UnsafeName_ReturnsTwo(string name)
    {
        using var store = await CreateStoreAsync();
        foreach (var input in new[] { name, new string('a', 129) })
        {
            var (exit, output, error) = await RunAsync(store, ["related", input]);
            Assert.Equal(2, exit);
            Assert.Empty(output);
            Assert.StartsWith($"error: invalid name '{input}'", error);
        }
    }

    [Theory]
    [InlineData("But", "did you mean: MudButton, MudBaseButton, MudIconButton")]
    [InlineData("Absent", "mudmcp search Absent --version 9.0.0")]
    [InlineData("color<T>", "mudmcp enum Color --version 9.0.0")]
    public async Task Related_MissingName_ReturnsFourWithHint(string name, string hint)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["related", name]);
        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: component '", error);
        Assert.EndsWith($"hint: {hint}\n", error);
    }

    [Theory]
    [InlineData("MudButton", "all")]
    [InlineData("MudBaseButton", "child")]
    [InlineData("MudButton", "commonly_used_with")]
    [InlineData("MudTextField", "all")]
    public async Task Related_Markdown_EqualsRenderRelated(string name, string type)
    {
        using var store = await CreateStoreAsync();
        var catalog = ReadStore(store).ReadCatalog().ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        var related = ComponentQueries.FindRelated(catalog, name, ComponentSearchTools.ParseRelationshipType(type));
        var (exit, output, error) = await RunAsync(store,
            ["related", name, "--type", type, "--format", "markdown"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(ComponentSearchTools.RenderRelated(catalog[name], related, "9.0.0"), output);
    }

    [Theory]
    [InlineData("compact", "MudTextField v9.0.0 related 0\n")]
    [InlineData("json", "[]\n")]
    public async Task Related_Empty_Succeeds(string format, string expected)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["related", "TextField", "--format", format]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(expected, output);
    }

    [Fact]
    public async Task Related_Compact_GroupsChildrenAndOther()
    {
        using var store = await CreateStoreAsync(transform: components =>
            components.Select(c => c.Name == "MudBaseButton"
                ? c with { RelatedComponents = ["MudTextField"] } : c).ToList());
        var (exit, output, error) = await RunAsync(store, ["related", "BaseButton"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudBaseButton v9.0.0 related 3\nchildren: MudButton, MudIconButton\nother: MudTextField\n", output);
    }

    [Fact]
    public async Task Search_Compact_OrdersByScoreThenName()
    {
        using var store = await CreateStoreAsync();

        var (exit, output, error) = await RunAsync(store, ["search", "button"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("\"button\" v9.0.0 search 3\n" +
            "MudButton [Buttons] Represents a button for actions, links, and commands.\n" +
            "MudBaseButton [-] The shared base type for button components.\n" +
            "MudIconButton [Buttons] A button that renders a single icon.\n", output);
    }

    [Fact]
    public async Task Search_JoinsPositionalsIntoQuery()
    {
        using var store = await CreateStoreAsync();

        var (exit, output, error) = await RunAsync(store, ["search", "text", "input"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("\"text input\" v9.0.0 search 1\n" +
            "MudTextField [Form & Inputs] A text input with validation and adornments.\n", output);
    }

    [Theory]
    [InlineData("name", "button", 3)]
    [InlineData("description", "validation", 1)]
    [InlineData("parameters", "Color", 1)]
    [InlineData("examples", "Loading", 1)]
    [InlineData("all", "Color", 1)]
    [InlineData("PaRaMeTeRs", "Color", 1)]
    [InlineData("name", "Color", 0)]
    public async Task Search_In_UsesSharedSearchFields(string searchIn, string query, int count)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store,
            ["search", query, "--in", searchIn, "--format", "json"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        using var json = JsonDocument.Parse(output);
        Assert.Equal(count, json.RootElement.GetArrayLength());
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public async Task Search_InvalidIn_ReturnsTwo(string searchIn)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["search", "button", "--in=" + searchIn]);
        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: ", error);
        Assert.Contains("--in", error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("51")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public async Task Search_LimitOutOfRange_ReturnsTwo(string limit)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["search", "button", "--limit=" + limit]);
        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: --limit", error);
    }

    [Theory]
    [InlineData("button", "all")]
    [InlineData("Color", "parameters")]
    [InlineData("missing", "all")]
    public async Task Search_Markdown_EqualsRenderSearchResults(string query, string searchIn)
    {
        using var store = await CreateStoreAsync();
        var fields = ComponentSearchTools.ParseSearchFields(searchIn);
        var results = ComponentQueries.Search(ReadStore(store).ReadCatalog(), query, fields, 2);

        var (exit, output, error) = await RunAsync(store,
            ["search", query, "--in", searchIn, "--limit", "2", "--format", "markdown"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(ComponentSearchTools.RenderSearchResults(query, results, fields, "9.0.0"), output);
    }

    [Fact]
    public async Task Search_Json_EmitsSummaries()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["search", "button", "--format", "json"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.EndsWith("\n", output);
        Assert.DoesNotContain('\n', output.TrimEnd('\n'));
        using var json = JsonDocument.Parse(output);
        var results = json.RootElement;
        Assert.Equal(3, results.GetArrayLength());
        var button = results[0];
        Assert.Equal("MudButton", button.GetProperty("name").GetString());
        Assert.Equal("Buttons", button.GetProperty("category").GetString());
        Assert.Equal("Represents a button for actions, links, and commands.", button.GetProperty("summary").GetString());
        Assert.Equal("MudBaseButton", button.GetProperty("baseType").GetString());
        Assert.Equal(6, button.GetProperty("parameterCount").GetInt32());
        Assert.Equal(1, button.GetProperty("eventCount").GetInt32());
        Assert.Equal(1, button.GetProperty("methodCount").GetInt32());
        Assert.Equal(2, button.GetProperty("exampleCount").GetInt32());
        Assert.Equal(8, button.EnumerateObject().Count());
        Assert.Equal("MudBaseButton", results[1].GetProperty("name").GetString());
        Assert.Equal("MudIconButton", results[2].GetProperty("name").GetString());
        Assert.Equal(["name", "summary"], results[1].EnumerateObject().Select(p => p.Name));
    }

    [Theory]
    [InlineData("compact", "\"missing\" v9.0.0 search 0\n")]
    [InlineData("json", "[]\n")]
    public async Task Search_NoResults_Succeeds(string format, string expected)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["search", "missing", "--format", format]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(expected, output);
    }

    [Theory]
    [InlineData("compact")]
    [InlineData("markdown")]
    [InlineData("json")]
    public async Task Search_Limits_DefaultTenAndBoundaries(string format)
    {
        using var store = await CreateStoreAsync(55);
        foreach (var limit in new int?[] { null, 1, 50 })
        {
            var args = new List<string> { "search", "button", "--format", format };
            if (limit is not null)
                args.AddRange(["--limit", limit.Value.ToString()]);
            var (exit, output, error) = await RunAsync(store, args.ToArray());
            Assert.Equal(0, exit);
            Assert.Empty(error);
            var count = limit ?? 10;
            if (format == "json")
            {
                using var json = JsonDocument.Parse(output);
                Assert.Equal(count, json.RootElement.GetArrayLength());
            }
            else if (format == "compact")
            {
                Assert.StartsWith($"\"button\" v9.0.0 search {count}\n", output);
                Assert.Equal(count + 1, output.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
            }
            else
            {
                Assert.Equal(ComponentSearchTools.RenderSearchResults("button",
                    ComponentQueries.Search(ReadStore(store).ReadCatalog(), "button", SearchFields.All, count),
                    SearchFields.All, "9.0.0"), output);
            }
        }
    }

    [Fact]
    public async Task Search_Compact_TruncatesSummaryAndCollapsesNewlines()
    {
        using var store = await CreateStoreAsync(summary: "first\r\n" + new string('x', 100));
        var (exit, output, error) = await RunAsync(store, ["search", "MudButton", "--in", "name"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("\"MudButton\" v9.0.0 search 1\nMudButton [Buttons] first " +
            new string('x', 71) + "...\n", output);
    }

    [Fact]
    public async Task Search_MissingQuery_ReturnsTwo()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["search"]);
        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: usage: mudmcp search <query...>", error);
    }

    [Fact]
    public async Task List_Compact_GroupsByCategoryIncludingUncategorized()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["list"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudBlazor v9.0.0 list 4\nButtons: MudButton, MudIconButton\n" +
            "Form & Inputs: MudTextField\nUncategorized: MudBaseButton\n", output);
    }

    [Theory]
    [InlineData("bUtToNs", "Buttons", "MudButton, MudIconButton", 2)]
    [InlineData("form & inputs", "Form & Inputs", "MudTextField", 1)]
    public async Task List_Category_FiltersAndUsesCategoryHeader(
        string input, string category, string names, int count)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["list", "--category", input]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal($"{category} v9.0.0 list {count}\n{category}: {names}\n", output);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("Button")]
    [InlineData(" Buttons ")]
    [InlineData("Uncategorized")]
    public async Task List_UnknownCategory_ReturnsFourListingCategories(string category)
    {
        using var store = await CreateStoreAsync();
        foreach (var format in new[] { "compact", "markdown", "json" })
        {
            var (exit, output, error) = await RunAsync(store,
                ["list", "--category=" + category, "--format", format]);
            Assert.Equal(4, exit);
            Assert.Empty(output);
            Assert.Equal($"error: category '{category}' not found\nhint: categories: Buttons, Form & Inputs\n", error);
        }
    }

    [Fact]
    public async Task List_Desc_OneLinePerComponent()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["list", "--desc"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudBlazor v9.0.0 list 4\n" +
            "MudBaseButton [-] The shared base type for button components.\n" +
            "MudButton [Buttons] Represents a button for actions, links, and commands.\n" +
            "MudIconButton [Buttons] A button that renders a single icon.\n" +
            "MudTextField [Form & Inputs] A text input with validation and adornments.\n", output);
    }

    [Fact]
    public async Task List_Desc_TruncatesSummaryAndCollapsesNewlines()
    {
        using var store = await CreateStoreAsync(summary: "first\r\n" + new string('x', 100));
        var (exit, output, error) = await RunAsync(store, ["list", "--category", "Buttons", "--desc"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("Buttons v9.0.0 list 2\nMudButton [Buttons] first " + new string('x', 71) +
            "...\nMudIconButton [Buttons] A button that renders a single icon.\n", output);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("bUtToNs", false)]
    [InlineData("bUtToNs", true)]
    public async Task List_Markdown_EqualsRenderComponentList(string? category, bool desc)
    {
        using var store = await CreateStoreAsync();
        var components = ReadStore(store).ReadCatalog();
        var args = new List<string> { "list", "--format", "markdown" };
        if (category is not null)
        {
            args.AddRange(["--category", category]);
            components = components.Where(c => c.Category == "Buttons").ToList();
        }
        if (desc)
            args.Add("--desc");
        var (exit, output, error) = await RunAsync(store, args.ToArray());
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(ComponentListTools.RenderComponentList(components,
            category is null ? null : "Buttons", desc, "9.0.0"), output);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task List_Json_EmitsSummaries(bool filtered, bool desc)
    {
        using var store = await CreateStoreAsync();
        var args = new List<string> { "list", "--format", "json" };
        if (filtered)
            args.AddRange(["--category", "buttons"]);
        if (desc)
            args.Add("--desc");
        var (exit, output, error) = await RunAsync(store, args.ToArray());
        Assert.Equal(0, exit);
        Assert.Empty(error);
        var expected = ReadStore(store).ReadCatalog().Where(c => !filtered || c.Category == "Buttons")
            .Select(ComponentSummary.From).ToList();
        Assert.Equal(JsonSerializer.Serialize<IReadOnlyList<ComponentSummary>>(expected,
            CliJsonContext.Default.IReadOnlyListComponentSummary) + "\n", output);
    }

    [Fact]
    public async Task Categories_Compact_CountsIncludingUncategorized()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["categories"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudBlazor v9.0.0 categories 3\nButtons 2\nForm & Inputs 1\nUncategorized 1\n", output);
    }

    [Fact]
    public async Task Categories_Markdown_EqualsRenderCategories()
    {
        using var store = await CreateStoreAsync();
        var catalog = ReadStore(store).ReadCatalog();
        var (exit, output, error) = await RunAsync(store, ["categories", "--format", "markdown"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(ComponentListTools.RenderCategories(ComponentQueries.BuildCategories(catalog),
            catalog, "9.0.0"), output);
    }

    [Fact]
    public async Task Categories_Json_IncludesUncategorized()
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, ["categories", "--format", "json"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("[{\"name\":\"Buttons\",\"title\":\"Buttons\",\"componentNames\":[\"MudButton\",\"MudIconButton\"]}," +
            "{\"name\":\"Form \\u0026 Inputs\",\"title\":\"Form \\u0026 Inputs\",\"componentNames\":[\"MudTextField\"]}," +
            "{\"name\":\"Uncategorized\",\"title\":\"Uncategorized\",\"componentNames\":[\"MudBaseButton\"]}]\n", output);
    }

    [Theory]
    [InlineData("compact")]
    [InlineData("markdown")]
    [InlineData("json")]
    public async Task Catalog_Empty_Succeeds(string format)
    {
        using var store = await CreateStoreAsync(copies: 0);
        foreach (var command in new[] { "list", "categories" })
        {
            var (exit, output, error) = await RunAsync(store, [command, "--format", format]);
            Assert.Equal(0, exit);
            Assert.Empty(error);
            var expected = format switch
            {
                "json" => "[]\n",
                "markdown" => command == "list"
                    ? ComponentListTools.RenderComponentList([], null, false, "9.0.0")
                    : ComponentListTools.RenderCategories([], [], "9.0.0"),
                _ => $"MudBlazor v9.0.0 {command} 0\n",
            };
            Assert.Equal(expected, output);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Catalog_OnlyUncategorized_CompactAndJson(string? category)
    {
        using var store = await CreateStoreAsync(transform: components =>
            components.Select(c => c with { Category = category }).ToList());
        var (_, list, _) = await RunAsync(store, ["list"]);
        Assert.Equal("MudBlazor v9.0.0 list 4\nUncategorized: MudBaseButton, MudButton, MudIconButton, MudTextField\n", list);
        var (exit, output, error) = await RunAsync(store, ["categories"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudBlazor v9.0.0 categories 1\nUncategorized 4\n", output);
        var (jsonExit, jsonOutput, jsonError) = await RunAsync(store, ["categories", "--format", "json"]);
        Assert.Equal(0, jsonExit);
        Assert.Empty(jsonError);
        using var json = JsonDocument.Parse(jsonOutput);
        Assert.Equal(1, json.RootElement.GetArrayLength());
        Assert.Equal("Uncategorized", json.RootElement[0].GetProperty("name").GetString());
        Assert.Equal(4, json.RootElement[0].GetProperty("componentNames").GetArrayLength());
    }

    [Fact]
    public async Task Categories_NoUncategorized_OmitsSyntheticCategory()
    {
        using var store = await CreateStoreAsync(copies: 2);
        var (exit, output, error) = await RunAsync(store, ["categories"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudBlazor v9.0.0 categories 1\nButtons 2\n", output);
        var (_, json, _) = await RunAsync(store, ["categories", "--format", "json"]);
        Assert.DoesNotContain("Uncategorized", json);
    }

    [Theory]
    [InlineData("list", "--limit=1")]
    [InlineData("list", "--category")]
    [InlineData("list", "--category=")]
    [InlineData("list", "extra")]
    [InlineData("categories", "--desc")]
    [InlineData("categories", "--category=Buttons")]
    [InlineData("categories", "extra")]
    public async Task Catalog_InvalidOptionsOrPositionals_ReturnsTwo(string command, string option)
    {
        using var store = await CreateStoreAsync();
        var (exit, output, error) = await RunAsync(store, [command, option]);
        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: ", error);
    }

    private static IndexStore ReadStore(CliTestStore store) =>
        new(new VersionContext("9.0.0", store.DataPath).StorePath);

    private static async Task<CliTestStore> CreateStoreAsync(int? copies = null, string? summary = null,
        Func<IReadOnlyList<ComponentInfo>, IReadOnlyList<ComponentInfo>>? transform = null)
    {
        var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        if (copies is not null || summary is not null || transform is not null)
        {
            var index = ReadStore(store);
            var button = index.TryReadComponent("MudButton")!;
            IReadOnlyList<ComponentInfo> components = copies is { } count
                ? Enumerable.Range(0, count).Select(i => button with { Name = $"MudButton{i:D2}" }).ToList()
                : index.ReadCatalog().Select(c => c.Name == button.Name ? c with { Summary = summary! } : c).ToList();
            if (transform is not null)
                components = transform(components);
            await IndexStore.WriteAsync(new VersionContext("9.0.0", store.DataPath).StorePath,
                index.TryReadManifest()!, components, index.ReadEnums(), TestContext.Current.CancellationToken);
        }

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
