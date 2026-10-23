// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using MudDocs.Cli;
using MudDocs.Configuration;
using MudDocs.Models;
using MudDocs.Services;
using MudDocs.Tools;

namespace MudDocs.Tests.Cli;

public class CliComponentCommandsTests
{
    [Fact]
    public async Task Component_NullPayload_ReturnsOne()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var path = Path.Combine(new VersionContext("9.0.0", store.DataPath).StorePath,
            "components", "mudbutton.json");
        await File.WriteAllTextAsync(path, "null", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, ["Button"]);

        Assert.Equal(1, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: ", error);
        Assert.Contains(path, error);
    }

    private const string ExpectedCompact =
        "MudButton v9.0.0 component\n" +
        "MudBlazor.MudButton : MudBaseButton [Buttons]\n" +
        "Represents a button for actions, links, and commands.\n" +
        "props 6 (own 4): Color, IconColor, Size, Variant\n" +
        "events 1: OnClick\n" +
        "methods 1: FocusAsync\n" +
        "examples 2: Filled, Loading\n" +
        "related: MudIconButton\n" +
        "docs: https://mudblazor.com/components/button\n";

    [Fact]
    public async Task Component_Compact_MatchesFmt005()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, ["MudButton"]);

        Assert.Empty(error);
        Assert.Equal(0, exit);
        Assert.Equal(ExpectedCompact, output);
    }

    [Theory]
    [InlineData("compact")]
    [InlineData("json")]
    public async Task Component_NoInherited_ExcludesInheritedMembers(string format)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var component = ReadButton(store);
        await WriteButtonAsync(store, component with
        {
            Events = [.. component.Events, new ComponentEvent("BaseEvent", null, null, true, "MudBaseButton")],
            Methods = [.. component.Methods, new ComponentMethod("BaseMethod", "void", null, [], false, true, "MudBaseButton")],
        });

        var (exit, output, error) = await RunAsync(store, ["MudButton", "--no-inherited", "--format", format]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.DoesNotContain("BaseEvent", output);
        Assert.DoesNotContain("BaseMethod", output);
        if (format == "compact")
        {
            Assert.Equal(ExpectedCompact.Replace("props 6", "props 4"), output);
        }
        else
        {
            using var json = JsonDocument.Parse(output);
            Assert.Equal(4, json.RootElement.GetProperty("parameters").GetArrayLength());
            Assert.All(json.RootElement.GetProperty("parameters").EnumerateArray(),
                parameter => Assert.False(parameter.TryGetProperty("isInherited", out _)));
            Assert.Single(json.RootElement.GetProperty("events").EnumerateArray());
            Assert.Single(json.RootElement.GetProperty("methods").EnumerateArray());
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Component_Markdown_EqualsRenderDetail(bool noInherited, bool noExamples)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        List<string> arguments = ["MudButton", "--format", "markdown"];
        if (noInherited)
            arguments.Add("--no-inherited");
        if (noExamples)
            arguments.Add("--no-examples");

        var (exit, output, error) = await RunAsync(store, arguments.ToArray());

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(ComponentDetailTools.RenderDetail(ReadButton(store), !noInherited, !noExamples, "9.0.0"), output);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Component_Json_HasNoExampleCode(bool noExamples)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        string[] arguments = noExamples
            ? ["MudButton", "--format", "json", "--no-examples"]
            : ["MudButton", "--format", "json"];

        var (exit, output, error) = await RunAsync(store, arguments);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.DoesNotContain("\n", output.TrimEnd('\r', '\n'));
        using var json = JsonDocument.Parse(output);
        Assert.Equal("MudButton", json.RootElement.GetProperty("name").GetString());
        Assert.Equal(6, json.RootElement.GetProperty("parameters").GetArrayLength());
        var examples = json.RootElement.GetProperty("examples");
        Assert.Equal(2, examples.GetArrayLength());
        Assert.Equal("Filled", examples[0].GetProperty("name").GetString());
        Assert.Equal(4, examples[0].GetProperty("features").GetArrayLength());
        Assert.All(examples.EnumerateArray(), example =>
        {
            Assert.False(example.TryGetProperty("razorMarkup", out _));
            Assert.False(example.TryGetProperty("cSharpCode", out _));
        });
        Assert.False(json.RootElement.TryGetProperty("description", out _));
        Assert.NotNull(ReadButton(store).Examples[1].CSharpCode);
    }

    [Fact]
    public async Task Component_NoExamples_OnlyAffectsMarkdown()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, ["MudButton", "--no-examples"]);

        Assert.Equal(0, exit);
        Assert.Equal(ExpectedCompact, output);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("Button")]
    [InlineData("button")]
    [InlineData("  mUdBuTtOn  ")]
    public async Task Component_ShortName_ResolvesMudPrefix(string name)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, [name]);

        Assert.Equal(0, exit);
        Assert.Equal(ExpectedCompact, output);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("MudButton<T>")]
    [InlineData("  Button<T>  ")]
    public async Task Component_GenericSuffix_IsStripped(string name)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, [name]);

        Assert.Equal(0, exit);
        Assert.Equal(ExpectedCompact, output);
        Assert.Empty(error);
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData(@"a\b")]
    [InlineData("")]
    [InlineData("Mud Button")]
    [InlineData("Büttön")]
    [InlineData("<T>")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task Component_InvalidName_ReturnsTwo(string name)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, [name]);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Equal($"error: invalid name '{name}'{Environment.NewLine}", error);
    }

    [Theory]
    [InlineData("But", "MudButton, MudBaseButton, MudIconButton")]
    [InlineData("mudBut", "MudButton, MudBaseButton, MudIconButton")]
    [InlineData("MudButtonExtra", "MudButton")]
    public async Task Component_Unknown_ReturnsFourWithSuggestions(string name, string suggestions)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, [name]);

        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.Equal($"error: component '{name}' not found for MudBlazor v9.0.0{Environment.NewLine}" +
            $"hint: did you mean: {suggestions}{Environment.NewLine}", error);
    }

    [Fact]
    public async Task Component_Unknown_NoCandidatesHintsSearch()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, ["NoSuchThing"]);

        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.Contains($"hint: muddocs search NoSuchThing --version 9.0.0{Environment.NewLine}", error);
    }

    [Fact]
    public async Task Component_Suggestions_AreLimitedAndBreakTiesByName()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var button = ReadButton(store);
        var catalog = new[] { "MudButz", "MudButb", "MudButa", "MudButc", "MudBute", "MudButd" }
            .Select(name => button with { Name = name }).ToList();
        await File.WriteAllTextAsync(
            Path.Combine(new VersionContext("9.0.0", store.DataPath).StorePath, "catalog.json"),
            JsonSerializer.Serialize(catalog, IndexStoreJsonContext.Default.ListComponentInfo),
            TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, ["But"]);

        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.Contains($"hint: did you mean: MudButa, MudButb, MudButc, MudButd, MudBute{Environment.NewLine}", error);
        Assert.DoesNotContain("MudButz", error);
    }

    [Theory]
    [InlineData("Color")]
    [InlineData("  color<T>  ")]
    public async Task Component_EnumName_HintsEnumCommand(string name)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, [name]);

        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.Contains($"hint: muddocs enum Color --version 9.0.0{Environment.NewLine}", error);
    }

    [Fact]
    public async Task Component_NotIndexed_ReturnsThreeWithIndexHint()
    {
        using var store = new CliTestStore();

        var (exit, output, error) = await RunAsync(store, ["MudButton"]);

        Assert.Equal(3, exit);
        Assert.Empty(output);
        Assert.Equal($"error: MudBlazor v9.0.0 is not indexed in {store.DataPath}{Environment.NewLine}" +
            $"hint: muddocs index --version 9.0.0{Environment.NewLine}", error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(store.DataPath));
    }

    [Fact]
    public async Task Component_WithoutVersion_ReturnsTwo()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exit = await CliApp.RunAsync(["component", "MudButton", "--data-path", store.DataPath],
            stdout, stderr, _ => null, TestContext.Current.CancellationToken);

        Assert.Equal(2, exit);
        Assert.Empty(stdout.ToString());
        Assert.Equal($"error: MudBlazor version required: pass --version X.Y.Z or set MUDBLAZOR_VERSION{Environment.NewLine}" +
            $"hint: indexed versions: 9.0.0{Environment.NewLine}", stderr.ToString());
    }

    [Fact]
    public async Task Component_EmptySections_AreOmitted()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunAsync(store, ["MudBaseButton"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudBaseButton v9.0.0 component\nMudBlazor.MudBaseButton\n" +
            "The shared base type for button components.\n", output);
    }

    [Fact]
    public async Task Component_Summary_IsFlattenedAndTruncated()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        await WriteButtonAsync(store, ReadButton(store) with { Summary = "First\r\n" + new string('x', 200) });

        var (exit, output, error) = await RunAsync(store, ["MudButton"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("First " + new string('x', 151) + "...", output.Split('\n')[2]);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("MudButton", "extra")]
    [InlineData("MudButton", "--props-only")]
    public async Task Component_InvalidArguments_ReturnTwo(string? name, string? extra)
    {
        using var store = new CliTestStore();
        string[] arguments = name is null ? [] : [name, extra!];

        var (exit, output, error) = await RunAsync(store, arguments);

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.StartsWith("error: ", error);
    }

    [Fact]
    public async Task Props_Compact_GroupsOwnThenInherited()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "props");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 props 6/6\n" +
            "Color: Color = Color.Default\nIconColor: Color = Color.Inherit\n" +
            "Size: Size = Size.Medium\nVariant: Variant = Variant.Text\n" +
            "-- inherited from MudBaseButton\nDisabled: bool\n" +
            "-- inherited from MudComponentBase\nClass: string?\n", output);
    }

    [Theory]
    [InlineData("color,SIZE", "Color,Size")]
    [InlineData("Col", "")]
    public Task Props_FilterExactNames(string filter, string names) =>
        AssertPropsNamesAsync(["--filter", filter], names);

    [Theory]
    [InlineData("*color", "Color,IconColor")]
    [InlineData("?ize,Varia??", "Size,Variant")]
    [InlineData("color,*COLOR", "Color,IconColor")]
    public Task Props_FilterGlob(string filter, string names) =>
        AssertPropsNamesAsync(["--filter", filter], names);

    [Fact]
    public async Task Props_RequiredOnly()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "props",
            ["--required-only"], "MudTextField");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudTextField v9.0.0 props 1/2\nValue: T? [required]\n", output);
        var cascading = await RunMemberAsync(store, "props", [], "MudTextField");
        Assert.Contains("Form: MudForm? [cascading]\n", cascading.Output);
    }

    [Theory]
    [InlineData("aPpEaRaNcE", "Color,IconColor,Size,Variant")]
    [InlineData("Appear", "")]
    public Task Props_CategoryFilter(string category, string names) =>
        AssertPropsNamesAsync(["--category", category], names);

    [Fact]
    public async Task Props_Limit_UpdatesShownCount()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "props", ["--filter", "*color,Size", "--limit", "2"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 props 2/6\nColor: Color = Color.Default\nIconColor: Color = Color.Inherit\n", output);
    }

    [Theory]
    [InlineData("props", "Color: Color = Color.Default")]
    [InlineData("events", "OnClick: EventCallback<MouseEventArgs>")]
    [InlineData("methods", "Task FocusAsync()")]
    public async Task Props_Desc_AppendsTruncatedDescription(string command, string line)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var component = ReadButton(store);
        var description = "First\r\n" + new string('x', 100);
        await WriteButtonAsync(store, component with
        {
            Parameters = component.Parameters.Select(p => p.Name == "Color" ? p with { Description = description } : p).ToList(),
            Events = [component.Events[0] with { Description = description }, new ComponentEvent("Simple", null, "")],
            Methods = [component.Methods[0] with { Description = description }, new ComponentMethod("Empty", "void", "", [], false)],
        });

        var (exit, output, error) = await RunMemberAsync(store, command, ["--desc"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Contains(line + "  // First " + new string('x', 71) + "...\n", output);
        Assert.Single(output.Split('\n'), value => value.Contains("//", StringComparison.Ordinal));
        var plain = await RunMemberAsync(store, command);
        Assert.DoesNotContain("//", plain.Output);
        Assert.DoesNotContain("First", plain.Output);
    }

    [Fact]
    public async Task Props_Markdown_EqualsRenderParameters()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "props",
            ["--category", "appearance", "--no-inherited", "--filter", "*Color,Size", "--limit", "2", "--format", "markdown"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(ComponentDetailTools.RenderParameters("MudButton",
            [ReadButton(store).Parameters[0], ReadButton(store).Parameters[3]], "9.0.0"), output);
    }

    [Fact]
    public Task Props_Json_EmitsFilteredList() =>
        AssertPropsNamesAsync(["--no-inherited", "--filter", "Size,*color", "--limit", "2"], "Color,IconColor");

    [Fact]
    public async Task Props_NoMatch_PrintsHeaderOnlyAndReturnsZero()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "props", ["--filter", "Absent"]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 props 0/6\n", output);
    }

    [Fact]
    public async Task Events_Compact_PrintsEventCallbackType()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var component = ReadButton(store);
        await WriteButtonAsync(store, component with
        {
            Events = [new ComponentEvent("Simple", null, null), .. component.Events],
        });

        var (exit, output, error) = await RunMemberAsync(store, "events");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 events 2/2\nOnClick: EventCallback<MouseEventArgs>\nSimple: EventCallback\n", output);
    }

    [Fact]
    public async Task Methods_Compact_PrintsSignature()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var component = ReadButton(store);
        await WriteButtonAsync(store, component with
        {
            Methods = [new ComponentMethod("Set", "void", null,
                [new MethodParameter("name", "string", null, null), new MethodParameter("count", "int", null, "1")], false),
                .. component.Methods],
        });

        var (exit, output, error) = await RunMemberAsync(store, "methods");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("MudButton v9.0.0 methods 2/2\nTask FocusAsync()\nvoid Set(string name, int count)\n", output);
    }

    [Theory]
    [InlineData("props", "compact")]
    [InlineData("props", "json")]
    [InlineData("props", "markdown")]
    [InlineData("events", "compact")]
    [InlineData("events", "json")]
    [InlineData("events", "markdown")]
    [InlineData("methods", "compact")]
    [InlineData("methods", "json")]
    [InlineData("methods", "markdown")]
    public async Task Members_FiltersOrderingLimitsAndFormats(string command, string format)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var component = ReadButton(store);
        (string Name, bool Inherited, string? DeclaringType)[] members =
        [
            ("zOther", true, "ZBase"), ("zOwn", false, null), ("zInherited", true, "ABase"),
            ("unknown", true, null), ("Alpha", false, null), ("aInherited", true, "ABase"),
        ];
        component = component with
        {
            Parameters = members.Select(m => new ComponentParameter(m.Name, "bool", null, null,
                true, true, "Behavior", m.Inherited, m.DeclaringType)).ToList(),
            Events = members.Select(m => new ComponentEvent(m.Name, null, null, m.Inherited, m.DeclaringType)).ToList(),
            Methods = members.Select(m => new ComponentMethod(m.Name, "void", null,
                [new MethodParameter("value", "int", null, null)], false, m.Inherited, m.DeclaringType)).ToList(),
        };
        await WriteButtonAsync(store, component);
        List<string> options = ["--format", format, "--limit", "5", "--filter", "*,Alpha"];
        if (command == "props")
            options.AddRange(["--category", "behavior", "--required-only"]);

        var (exit, output, error) = await RunMemberAsync(store, command, options.ToArray());
        string[] ordered = ["Alpha", "zOwn", "aInherited", "zInherited", "unknown"];

        Assert.Equal(0, exit);
        Assert.Empty(error);
        if (format == "json")
        {
            using var json = JsonDocument.Parse(output);
            Assert.Equal(ordered, json.RootElement.EnumerateArray().Select(item => item.GetProperty("name").GetString()));
            Assert.DoesNotContain("\n", output.TrimEnd('\r', '\n'));
            Assert.All(json.RootElement.EnumerateArray(), item =>
                Assert.False(item.TryGetProperty("description", out _)));
        }
        else if (format == "compact")
        {
            string Line(string name) => command switch
            {
                "props" => $"{name}: bool [required] [cascading]\n",
                "events" => $"{name}: EventCallback\n",
                _ => $"void {name}(int value)\n",
            };
            Assert.Equal($"MudButton v9.0.0 {command} 5/6\n" +
                Line("Alpha") + Line("zOwn") + "-- inherited from ABase\n" +
                Line("aInherited") + Line("zInherited") + "-- inherited from base\n" + Line("unknown"), output);
        }
        else
        {
            Assert.Equal(ExpectedMemberMarkdown(component, command, ordered), output);
        }

        options.Add("--no-inherited");
        var own = await RunMemberAsync(store, command, options.ToArray());
        Assert.Equal(0, own.Exit);
        Assert.Empty(own.Error);
        Assert.DoesNotContain("aInherited", own.Output);
        Assert.DoesNotContain("unknown", own.Output);
        Assert.Contains("Alpha", own.Output);
        Assert.Contains("zOwn", own.Output);
    }

    [Theory]
    [InlineData("props", "compact")]
    [InlineData("props", "json")]
    [InlineData("props", "markdown")]
    [InlineData("events", "compact")]
    [InlineData("events", "json")]
    [InlineData("events", "markdown")]
    [InlineData("methods", "compact")]
    [InlineData("methods", "json")]
    [InlineData("methods", "markdown")]
    public async Task Members_EmptyOutputAndLimitBoundaries(string command, string format)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var component = ReadButton(store);
        foreach (var emptyComponent in new[] { false, true })
        {
            var (exit, output, error) = await RunMemberAsync(store, command,
                ["--filter", "absent", "--limit", "1000", "--format", format],
                emptyComponent ? "MudBaseButton" : "MudButton");
            Assert.Equal(0, exit);
            Assert.Empty(error);
            var subject = emptyComponent ? ReadBaseButton(store) : component;
            var total = emptyComponent ? 0 : command == "props" ? 6 : 1;
            Assert.Equal(format switch
            {
                "json" => "[]\n",
                "markdown" => ExpectedMemberMarkdown(subject, command, []),
                _ => $"{subject.Name} v9.0.0 {command} 0/{total}\n",
            }, output);
        }

        var limited = await RunMemberAsync(store, command, ["--limit", "1", "--format", format]);
        Assert.Equal(0, limited.Exit);
        Assert.Empty(limited.Error);
        if (format == "json")
        {
            using var json = JsonDocument.Parse(limited.Output);
            Assert.Single(json.RootElement.EnumerateArray());
        }
        else if (format == "compact")
            Assert.StartsWith($"MudButton v9.0.0 {command} 1/", limited.Output);
        else
            Assert.Equal(ExpectedMemberMarkdown(component, command,
                [command == "props" ? "Color" : command == "events" ? "OnClick" : "FocusAsync"]), limited.Output);
    }

    [Theory]
    [InlineData("props")]
    [InlineData("events")]
    [InlineData("methods")]
    public async Task Members_InvalidLimitsAndOptions_ReturnTwo(string command)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        foreach (var limit in new[] { "0", "-1", "1001", "abc", "1.5", "2147483648" })
        {
            var (exit, output, error) = await RunMemberAsync(store, command, ["--limit", limit]);
            Assert.Equal(2, exit);
            Assert.Empty(output);
            Assert.Contains("--limit", error);
        }
        if (command != "props")
        {
            foreach (var option in new[] { "--category=Behavior", "--required-only" })
            {
                var result = await RunMemberAsync(store, command, [option]);
                Assert.Equal(2, result.Exit);
                Assert.Empty(result.Output);
                Assert.Contains("unknown option", result.Error);
            }
        }
    }

    [Theory]
    [InlineData("compact")]
    [InlineData("markdown")]
    [InlineData("json")]
    public async Task Props_CombinedFilters_PrecedeLimit(string format)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var component = ReadButton(store);
        component = component with
        {
            Parameters =
            [
                new ComponentParameter("A", "int", null, null, true, false, "Other"),
                new ComponentParameter("B", "int", null, null, false, false, "Behavior"),
                new ComponentParameter("C", "int", null, null, true, false, "Behavior", true, "Base"),
                new ComponentParameter("D", "int", null, null, true, false, "Behavior"),
                new ComponentParameter("Zulu", "int", null, null, true, false, "Behavior"),
                new ComponentParameter("Echo", "int", null, null, true, false, "Behavior"),
            ],
        };
        await WriteButtonAsync(store, component);

        var (exit, output, error) = await RunMemberAsync(store, "props",
            ["--category", "behavior", "--required-only", "--no-inherited", "--filter", "?cho,Z*", "--limit", "1", "--format", format]);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(format switch
        {
            "compact" => "MudButton v9.0.0 props 1/6\nEcho: int [required]\n",
            "markdown" => ExpectedMemberMarkdown(component, "props", ["Echo"]),
            _ => JsonSerializer.Serialize((IReadOnlyList<ComponentParameter>)[component.Parameters[5]],
                CliJsonContext.Default.Options) + "\n",
        }, output);
    }

    [Theory]
    [InlineData("all", "compact")]
    [InlineData("properties", "compact")]
    [InlineData("events", "compact")]
    [InlineData("methods", "compact")]
    [InlineData("all", "markdown")]
    [InlineData("properties", "markdown")]
    [InlineData("events", "markdown")]
    [InlineData("methods", "markdown")]
    [InlineData("all", "json")]
    [InlineData("properties", "json")]
    [InlineData("events", "json")]
    [InlineData("methods", "json")]
    public async Task Api_Component_PrintsSections(string members, string format)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var component = ReadButton(store);
        component = component with
        {
            Methods = [new ComponentMethod("Set", "void", null,
                [new MethodParameter("value", "int", null, null)], false), .. component.Methods],
        };
        await WriteButtonAsync(store, component);
        var expectedApi = new ApiReference("MudButton", "MudBlazor", component.Summary, "MudBaseButton",
        [
            .. component.Parameters.Select(p => new ApiMember(p.Name, "Property", p.Type, p.Description)),
            new ApiMember("OnClick", "Event", "EventCallback<MouseEventArgs>", null),
            new ApiMember("Set", "Method", "void", null, "int value"),
            new ApiMember("FocusAsync", "Method", "Task", null),
        ]);

        var (exit, output, error) = await RunMemberAsync(store, "api",
            ["--members", members, "--format", format], "  button<T>  ");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        if (format == "markdown")
            Assert.Equal(ApiReferenceTools.RenderApiReference(expectedApi, members, "9.0.0"), output);
        else if (format == "json")
        {
            using var json = JsonDocument.Parse(output);
            Assert.Equal("MudButton", json.RootElement.GetProperty("typeName").GetString());
            var expectedMembers = expectedApi.Members!.Where(m => members == "all" ||
                m.MemberType == (members == "properties" ? "Property" : members == "events" ? "Event" : "Method"));
            Assert.Equal(expectedMembers.Select(m => m.Name),
                json.RootElement.GetProperty("members").EnumerateArray().Select(m => m.GetProperty("name").GetString()));
            Assert.False(json.RootElement.TryGetProperty("examples", out _));
            Assert.DoesNotContain("\n", output.TrimEnd('\n'));
        }
        else
        {
            var expected = "MudButton v9.0.0 api\n";
            if (members is "all" or "properties")
                expected += "props 6\nColor: Color = Color.Default\nIconColor: Color = Color.Inherit\n" +
                    "Size: Size = Size.Medium\nVariant: Variant = Variant.Text\n" +
                    "-- inherited from MudBaseButton\nDisabled: bool\n" +
                    "-- inherited from MudComponentBase\nClass: string?\n";
            if (members is "all" or "events")
                expected += "events 1\nOnClick: EventCallback<MouseEventArgs>\n";
            if (members is "all" or "methods")
                expected += "methods 2\nTask FocusAsync()\nvoid Set(int value)\n";
            Assert.Equal(expected, output);
        }
    }

    [Theory]
    [InlineData("compact", false)]
    [InlineData("compact", true)]
    [InlineData("markdown", false)]
    [InlineData("markdown", true)]
    [InlineData("json", false)]
    [InlineData("json", true)]
    public async Task Api_EnumFallback_PrintsEnum(string format, bool desc)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        string[] options = desc ? ["--format", format, "--desc"] : ["--format", format];

        var expected = await RunMemberAsync(store, "enum", options, "Size");
        var actual = await RunMemberAsync(store, "api", [.. options, "--members", "methods"], "  sIzE<T>  ");

        Assert.Equal(0, expected.Exit);
        Assert.Empty(expected.Error);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("But", "did you mean: MudButton, MudBaseButton, MudIconButton")]
    [InlineData("Col", "did you mean: Color")]
    [InlineData("Unknown", "muddocs search Unknown --version 9.0.0")]
    public async Task Api_Unknown_ReturnsFour(string name, string hint)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "api", name: name);

        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.Equal($"error: type '{name}' not found for MudBlazor v9.0.0{Environment.NewLine}" +
            $"hint: {hint}{Environment.NewLine}", error);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("property")]
    [InlineData("")]
    public async Task Api_InvalidMembers_ReturnsTwo(string members)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "api", ["--members", members], "Color");

        Assert.Equal(2, exit);
        Assert.Empty(output);
        Assert.Contains("--members", error);
    }

    [Theory]
    [InlineData("Color", "Default, Primary, Secondary")]
    [InlineData("Size", "Small=0, Medium=1, Large=2")]
    public async Task Enum_Compact_SingleLineWithExplicitValues(string name, string values)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "enum", name: name);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal($"{name} v9.0.0 enum 3: {values}\n", output);
    }

    [Fact]
    public async Task Enum_Desc_OneValuePerLine()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var info = new EnumInfo("Color", null, null,
        [
            new EnumValueInfo("Default", 0, "First\r\n" + new string('x', 100)),
            new EnumValueInfo("Primary", -7, null),
            new EnumValueInfo("Secondary", null, ""),
        ]);
        await File.WriteAllTextAsync(
            Path.Combine(new VersionContext("9.0.0", store.DataPath).StorePath, "enums.json"),
            JsonSerializer.Serialize(new List<EnumInfo> { info }, IndexStoreJsonContext.Default.ListEnumInfo),
            TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "enum", ["--desc"], "  color<T>  ");

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal("Color v9.0.0 enum 3\nDefault=0  // First " + new string('x', 71) +
            "...\nPrimary=-7\nSecondary\n", output);
    }

    [Theory]
    [InlineData("Color")]
    [InlineData("Size")]
    public async Task Enum_Markdown_EqualsRenderEnum(string name)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var info = new IndexStore(new VersionContext("9.0.0", store.DataPath).StorePath).TryReadEnum(name)!;

        var (exit, output, error) = await RunMemberAsync(store, "enum", ["--format", "markdown"], name);

        Assert.Equal(0, exit);
        Assert.Empty(error);
        Assert.Equal(ApiReferenceTools.RenderEnum(info, "9.0.0"), output);
    }

    [Fact]
    public async Task Enum_Json_UsesCamelCaseAndOmitsDefaults()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var result = await RunMemberAsync(store, "enum", ["--format", "json"], "size");

        Assert.Equal(0, result.Exit);
        Assert.Empty(result.Error);
        Assert.Equal("{\"name\":\"Size\",\"namespace\":\"MudBlazor\",\"summary\":\"The component sizes.\"," +
            "\"values\":[{\"name\":\"Small\",\"value\":0},{\"name\":\"Medium\",\"value\":1},{\"name\":\"Large\",\"value\":2}]}\n",
            result.Output);
    }

    [Theory]
    [InlineData("Col", "did you mean: Color")]
    [InlineData("But", "muddocs search But --version 9.0.0")]
    [InlineData("MudColor", "did you mean: Color")]
    public async Task Enum_Unknown_ReturnsFourWithSuggestions(string name, string hint)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var (exit, output, error) = await RunMemberAsync(store, "enum", name: name);

        Assert.Equal(4, exit);
        Assert.Empty(output);
        Assert.Equal($"error: enum '{name}' not found for MudBlazor v9.0.0{Environment.NewLine}" +
            $"hint: {hint}{Environment.NewLine}", error);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("enum")]
    public async Task ApiAndEnum_InvalidNames_ReturnTwo(string command)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        foreach (var name in new[] { "../x", @"a\b", "", "Mud Button", "Büttön", "<T>", new string('x', 129) })
        {
            var result = await RunMemberAsync(store, command, name: name);
            Assert.Equal(2, result.Exit);
            Assert.Empty(result.Output);
            Assert.Equal($"error: invalid name '{name}'{Environment.NewLine}", result.Error);
        }
    }

    [Fact]
    public async Task Api_Desc_PreservesMemberAnnotationsAndEmptySections()
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);

        var result = await RunMemberAsync(store, "api", ["--members", "properties", "--desc"], "MudButton");
        Assert.Equal(0, result.Exit);
        Assert.Empty(result.Error);
        Assert.Contains("Color: Color = Color.Default  // The color of the button.\n", result.Output);
        var fields = await RunMemberAsync(store, "api", name: "MudTextField");
        Assert.Equal(0, fields.Exit);
        Assert.Contains("Value: T? [required]\n", fields.Output);
        Assert.Contains("Form: MudForm? [cascading]\n", fields.Output);
        Assert.DoesNotContain("events 0", fields.Output);
        var empty = await RunMemberAsync(store, "api", name: "MudBaseButton");
        Assert.Equal(0, empty.Exit);
        Assert.Equal("MudBaseButton v9.0.0 api\n", empty.Output);
    }

    private static ComponentInfo ReadBaseButton(CliTestStore store) =>
        new IndexStore(new VersionContext("9.0.0", store.DataPath).StorePath).TryReadComponent("MudBaseButton")!;

    private static string ExpectedMemberMarkdown(ComponentInfo component, string command, string[] names)
    {
        if (command == "props")
            return ComponentDetailTools.RenderParameters(component.Name,
                names.Select(name => component.Parameters.Single(p => p.Name == name)).ToList(), "9.0.0");
        var members = names.Select(name => command == "events"
            ? component.Events.Where(e => e.Name == name).Select(e => new ApiMember(e.Name, "Event",
                e.EventArgsType is null ? "EventCallback" : $"EventCallback<{e.EventArgsType}>", e.Description)).Single()
            : component.Methods.Where(m => m.Name == name).Select(m => new ApiMember(m.Name, "Method",
                m.ReturnType, m.Description, m.Parameters.Count == 0 ? null :
                    string.Join(", ", m.Parameters.Select(p => $"{p.Type} {p.Name}")))).Single()).ToList();
        return ApiReferenceTools.RenderApiReference(new ApiReference(component.Name, component.Namespace,
            component.Summary, component.BaseType, members), command, "9.0.0");
    }

    private static async Task AssertPropsNamesAsync(string[] options, string names)
    {
        using var store = new CliTestStore();
        await store.WriteReady("9.0.0", TestContext.Current.CancellationToken);
        var (exit, output, error) = await RunMemberAsync(store, "props", [.. options, "--format", "json"]);
        Assert.Equal(0, exit);
        Assert.Empty(error);
        using var json = JsonDocument.Parse(output);
        Assert.Equal(names.Length == 0 ? [] : names.Split(','),
            json.RootElement.EnumerateArray().Select(item => item.GetProperty("name").GetString()));
    }

    private static async Task<(int Exit, string Output, string Error)> RunMemberAsync(
        CliTestStore store, string command, string[]? options = null, string name = "MudButton")
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(
            [command, name, .. options ?? [], "--version", "9.0.0", "--data-path", store.DataPath],
            stdout, stderr, _ => null, TestContext.Current.CancellationToken);
        return (exit, stdout.ToString(), stderr.ToString());
    }

    private static ComponentInfo ReadButton(CliTestStore store) =>
        new IndexStore(new VersionContext("9.0.0", store.DataPath).StorePath).TryReadComponent("MudButton")!;

    private static Task WriteButtonAsync(CliTestStore store, ComponentInfo component) =>
        File.WriteAllTextAsync(
            Path.Combine(new VersionContext("9.0.0", store.DataPath).StorePath, "components", "mudbutton.json"),
            JsonSerializer.Serialize(component, IndexStoreJsonContext.Default.ComponentInfo),
            TestContext.Current.CancellationToken);

    private static async Task<(int Exit, string Output, string Error)> RunAsync(CliTestStore store, string[] arguments)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exit = await CliApp.RunAsync(
            ["component", .. arguments, "--version", "9.0.0", "--data-path", store.DataPath],
            stdout, stderr, _ => null, TestContext.Current.CancellationToken);
        return (exit, stdout.ToString(), stderr.ToString());
    }
}
