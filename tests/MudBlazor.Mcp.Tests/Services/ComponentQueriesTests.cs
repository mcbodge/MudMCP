// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Tests.Services;

public class ComponentQueriesTests
{
    private static ComponentInfo CreateComponent(
        string name,
        string? category = null,
        string? baseType = null,
        string summary = "",
        string? description = null)
    {
        return new ComponentInfo(
            Name: name,
            Namespace: "MudBlazor",
            Summary: summary,
            Description: description,
            Category: category,
            BaseType: baseType,
            Parameters: [],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null);
    }

    [Fact]
    public void BuildCategories_GroupsAndSortsByName()
    {
        // Arrange - components across multiple categories (out of order), plus one with an empty category
        var components = new[]
        {
            CreateComponent("MudText", category: "Typography"),
            CreateComponent("MudTextField", category: "Inputs"),
            CreateComponent("MudButton", category: "Inputs"),
            CreateComponent("MudChart", category: "Charts"),
            CreateComponent("MudOrphan", category: ""),
        };

        // Act
        var categories = ComponentQueries.BuildCategories(components);

        // Assert - categories sorted by name (ordinal-ignore-case), empty-category component excluded
        Assert.Equal(3, categories.Count);
        Assert.Equal("Charts", categories[0].Name);
        Assert.Equal("Inputs", categories[1].Name);
        Assert.Equal("Typography", categories[2].Name);

        // Each category's component names are sorted
        string[] expectedInputs = ["MudButton", "MudTextField"];
        Assert.Equal(expectedInputs, categories[1].ComponentNames);

        Assert.DoesNotContain(categories, c => c.ComponentNames.Contains("MudOrphan"));
    }

    [Fact]
    public void Search_OrdersByScoreThenName()
    {
        // Arrange - two components with the SAME score for "mud", supplied out of name order
        var components = new[]
        {
            CreateComponent("MudZebra"),
            CreateComponent("MudAlpha"),
        };

        // Act
        var results = ComponentQueries.Search(components, "mud", SearchFields.Name, maxResults: 10);

        // Assert - equal scores are tie-broken by name (ordinal-ignore-case), lower name first
        Assert.Equal(2, results.Count);
        Assert.Equal("MudAlpha", results[0].Name);
        Assert.Equal("MudZebra", results[1].Name);
    }

    [Fact]
    public void Search_HonorsFieldsAndMaxResults()
    {
        // Arrange - one component matches only in its description, two match by name
        var descriptionOnly = CreateComponent("MudButton", category: "Inputs", description: "A clickable widget for actions");
        var nameMatchA = CreateComponent("MudWidgetAlpha", category: "Inputs");
        var nameMatchB = CreateComponent("MudWidgetBeta", category: "Inputs");
        var components = new[] { descriptionOnly, nameMatchA, nameMatchB };

        // Act
        var nameField = ComponentQueries.Search(components, "widget", SearchFields.Name, maxResults: 10);
        var descriptionField = ComponentQueries.Search(components, "widget", SearchFields.Description, maxResults: 10);
        var capped = ComponentQueries.Search(components, "widget", SearchFields.All, maxResults: 2);

        // Assert - the description-only match is ignored when searching Name, honored when searching Description
        Assert.DoesNotContain(nameField, c => c.Name == "MudButton");
        Assert.Contains(descriptionField, c => c.Name == "MudButton");

        // And maxResults caps the number of returned matches
        Assert.Equal(2, capped.Count);
    }

    [Fact]
    public void FindRelated_ReturnsParentChildrenAndSiblingsInNameOrder()
    {
        // Arrange - a target with a parent (base type), a child, and two same-category siblings out of order
        var components = new Dictionary<string, ComponentInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["MudList"] = CreateComponent("MudList", category: "Lists", baseType: "MudBaseList"),
            ["MudBaseList"] = CreateComponent("MudBaseList", category: "Base"),
            ["MudListItem"] = CreateComponent("MudListItem", category: "ListItems", baseType: "MudList"),
            ["MudSelect"] = CreateComponent("MudSelect", category: "Lists"),
            ["MudAutocomplete"] = CreateComponent("MudAutocomplete", category: "Lists"),
        };

        // Act
        var related = ComponentQueries.FindRelated(components, "MudList", RelationshipType.All);

        // Assert - parent, child, and siblings are all present
        var names = related.Select(c => c.Name).ToList();
        Assert.Contains("MudBaseList", names);
        Assert.Contains("MudListItem", names);
        Assert.Contains("MudAutocomplete", names);
        Assert.Contains("MudSelect", names);

        // Siblings appear in name order (ordinal-ignore-case)
        Assert.True(names.IndexOf("MudAutocomplete") < names.IndexOf("MudSelect"));
    }

    [Fact]
    public void FindRelated_UnknownName_ReturnsEmpty()
    {
        // Arrange
        var components = new Dictionary<string, ComponentInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["MudButton"] = CreateComponent("MudButton", category: "Inputs"),
        };

        // Act
        var related = ComponentQueries.FindRelated(components, "MudDoesNotExist", RelationshipType.All);

        // Assert
        Assert.Empty(related);
    }

    [Fact]
    public void GroupRelated_SplitsParentChildrenSiblingsAndOthers()
    {
        // Arrange - a target with a parent (base type), a child, a same-category sibling, and an unrelated other
        var target = CreateComponent("MudList", category: "Lists", baseType: "MudBaseList");
        IReadOnlyList<ComponentInfo> related =
        [
            CreateComponent("MudBaseList", category: "Base"),
            CreateComponent("MudListItem", category: "ListItems", baseType: "MudList"),
            CreateComponent("MudSelect", category: "Lists"),
            CreateComponent("MudOther", category: "Misc"),
        ];

        // Act
        var (parent, children, siblings, others) = ComponentQueries.GroupRelated(target, related);

        // Assert - each related component lands in exactly one bucket
        Assert.Equal("MudBaseList", parent!.Name);
        Assert.Contains(children, c => c.Name == "MudListItem");
        Assert.Contains(siblings, c => c.Name == "MudSelect");
        Assert.Contains(others, c => c.Name == "MudOther");

        // Buckets are disjoint for these names
        Assert.DoesNotContain(children, c => c.Name is "MudBaseList" or "MudSelect" or "MudOther");
        Assert.DoesNotContain(siblings, c => c.Name is "MudBaseList" or "MudListItem" or "MudOther");
        Assert.DoesNotContain(others, c => c.Name is "MudBaseList" or "MudListItem" or "MudSelect");
    }
}
