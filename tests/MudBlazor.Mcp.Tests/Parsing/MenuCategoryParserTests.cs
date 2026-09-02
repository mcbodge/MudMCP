// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Services.Parsing;

namespace MudBlazor.Mcp.Tests.Parsing;

public class MenuCategoryParserTests
{
    // Mirrors the structure of MudBlazor's MenuService._docsComponents fluent chain:
    // top-level AddItem entries are ungrouped, AddNavGroup wraps grouped items, and
    // items may carry child types as additional typeof arguments.
    private const string SampleMenu = """
        namespace MudBlazor.Docs.Services
        {
            public class MenuService
            {
                private readonly List<MudComponent> _docsComponents = new DocsComponents()
                    .AddItem("Container", typeof(MudContainer))
                    .AddItem("Grid", typeof(MudGrid), typeof(MudItem))
                    .AddItem("Chips", typeof(MudChip<T>))
                    .AddNavGroup("Form & Inputs", false, new DocsComponents()
                        .AddItem("Radio", typeof(MudRadio<T>), typeof(MudRadioGroup<T>))
                        .AddItem("Text Field", typeof(MudTextField<T>))
                    )
                    .AddNavGroup("Buttons", false, new DocsComponents()
                        .AddItem("Button", typeof(MudButton))
                        .AddItem("Icon Button", typeof(MudIconButton))
                    )
                    .GetComponentsSortedByName();
            }
        }
        """;

    [Fact]
    public void ParseMenuSource_TopLevelItems_MapToComponents()
    {
        var map = MenuCategoryParser.ParseMenuSource(SampleMenu);

        Assert.Equal("Components", map["MudContainer"]);
        Assert.Equal("Components", map["MudGrid"]);
    }

    [Fact]
    public void ParseMenuSource_ChildTypes_InheritTheirItemCategory()
    {
        var map = MenuCategoryParser.ParseMenuSource(SampleMenu);

        // MudItem is a child typeof under the top-level "Grid" item.
        Assert.Equal("Components", map["MudItem"]);
        // MudRadioGroup is a child under the "Radio" item within the Form & Inputs group.
        Assert.Equal("Form & Inputs", map["MudRadioGroup"]);
    }

    [Fact]
    public void ParseMenuSource_GenericTypes_StripArity()
    {
        var map = MenuCategoryParser.ParseMenuSource(SampleMenu);

        Assert.Equal("Components", map["MudChip"]);
        Assert.Equal("Form & Inputs", map["MudTextField"]);
    }

    [Fact]
    public void ParseMenuSource_NavGroupItems_MapToGroupName()
    {
        var map = MenuCategoryParser.ParseMenuSource(SampleMenu);

        Assert.Equal("Buttons", map["MudButton"]);
        Assert.Equal("Buttons", map["MudIconButton"]);
        Assert.Equal("Form & Inputs", map["MudRadio"]);
    }

    [Fact]
    public void ParseMenuSource_UnlistedComponent_IsAbsent()
    {
        var map = MenuCategoryParser.ParseMenuSource(SampleMenu);

        Assert.False(map.ContainsKey("MudUnknownWidget"));
    }

    [Fact]
    public void GetCategoryName_WithoutInitialization_ReturnsNull()
    {
        var parser = new MenuCategoryParser(NullLogger<MenuCategoryParser>.Instance);

        Assert.Null(parser.GetCategoryName("MudButton"));
    }
}
