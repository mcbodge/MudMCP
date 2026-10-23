// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudDocs.Configuration;
using MudDocs.Models;
using MudDocs.Services;
using MudDocs.Tools;

namespace MudDocs.Tests.Tools;

public class ComponentExampleToolsTests
{
    private static readonly ILogger<ComponentExampleTools> NullLogger =
        NullLoggerFactory.Instance.CreateLogger<ComponentExampleTools>();

    private static readonly VersionContext _versionContext = new("9.0.0");

    #region GetComponentExamplesAsync Tests

    [Fact]
    public async Task GetComponentExamplesAsync_WithValidComponent_ReturnsExamples()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act
        var result = await ComponentExampleTools.GetComponentExamplesAsync(
            indexer, NullLogger, _versionContext, "MudButton", 5, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("Examples", result);
        Assert.Contains("Basic Button", result);
    }

    [Fact]
    public async Task GetComponentExamplesAsync_WithNullMaxExamples_UsesDefaultValue()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act - simulating what happens when MCP client doesn't send maxExamples
        var result = await ComponentExampleTools.GetComponentExamplesAsync(
            indexer, NullLogger, _versionContext, "MudButton", null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("Examples", result);
    }

    [Fact]
    public async Task GetComponentExamplesAsync_WithFilter_ReturnsFilteredExamples()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act
        var result = await ComponentExampleTools.GetComponentExamplesAsync(
            indexer, NullLogger, _versionContext, "MudButton", 5, "icon", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Icon Button", result);
    }

    [Fact]
    public async Task GetComponentExamplesAsync_WithEmptyComponentName_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();

        // Act & Assert
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentExampleTools.GetComponentExamplesAsync(
                indexer.Object, NullLogger, _versionContext, "", 5, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetComponentExamplesAsync_WithNullComponentName_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();

        // Act & Assert
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentExampleTools.GetComponentExamplesAsync(
                indexer.Object, NullLogger, _versionContext, null!, 5, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetComponentExamplesAsync_WithInvalidComponent_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.GetComponentAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComponentInfo?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentExampleTools.GetComponentExamplesAsync(
                indexer.Object, NullLogger, _versionContext, "Unknown", 5, null, TestContext.Current.CancellationToken));

        Assert.Contains("not found", ex.Message);
        Assert.Contains("list_components", ex.Message);
    }

    [Fact]
    public async Task GetComponentExamplesAsync_WithMaxExamplesOutOfRange_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act & Assert - maxExamples = 0 is out of range (min is 1)
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentExampleTools.GetComponentExamplesAsync(
                indexer, NullLogger, _versionContext, "MudButton", 0, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetComponentExamplesAsync_WithMaxExamplesTooHigh_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act & Assert - maxExamples = 100 is out of range (max is 20)
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentExampleTools.GetComponentExamplesAsync(
                indexer, NullLogger, _versionContext, "MudButton", 100, null, TestContext.Current.CancellationToken));
    }

    #endregion

    #region GetExampleByNameAsync Tests

    [Fact]
    public async Task GetExampleByNameAsync_WithValidExample_ReturnsExample()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act
        var result = await ComponentExampleTools.GetExampleByNameAsync(
            indexer, NullLogger, _versionContext, "MudButton", "Basic Button", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("Basic Button", result);
        Assert.Contains("<MudButton>Click Me</MudButton>", result);
    }

    [Fact]
    public async Task GetExampleByNameAsync_WithFuzzyMatch_ReturnsExample()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act
        var result = await ComponentExampleTools.GetExampleByNameAsync(
            indexer, NullLogger, _versionContext, "MudButton", "Basic", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Basic Button", result);
    }

    [Fact]
    public async Task GetExampleByNameAsync_WithInvalidExample_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentExampleTools.GetExampleByNameAsync(
                indexer, NullLogger, _versionContext, "MudButton", "NonExistent", TestContext.Current.CancellationToken));

        Assert.Contains("not found", ex.Message);
    }

    #endregion

    #region ListComponentExamplesAsync Tests

    [Fact]
    public async Task ListComponentExamplesAsync_WithValidComponent_ReturnsExampleList()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act
        var result = await ComponentExampleTools.ListComponentExamplesAsync(
            indexer, NullLogger, _versionContext, "MudButton", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("Basic Button", result);
        Assert.Contains("Icon Button", result);
        Assert.Contains("3 example(s)", result);
    }

    [Fact]
    public async Task ListComponentExamplesAsync_WithNoExamples_ReturnsNoExamplesMessage()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();
        var component = new ComponentInfo(
            Name: "MudEmpty",
            Namespace: "MudBlazor",
            Summary: "An empty component",
            Description: null,
            Category: "Test",
            BaseType: null,
            Parameters: [],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null
        );
        indexer.Setup(x => x.GetComponentAsync("MudEmpty", It.IsAny<CancellationToken>()))
            .ReturnsAsync(component);

        // Act
        var result = await ComponentExampleTools.ListComponentExamplesAsync(
            indexer.Object, NullLogger, _versionContext, "MudEmpty", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("No examples available", result);
    }

    #endregion

    #region Refactoring Extraction Tests (TASK-008)

    [Fact]
    public async Task RenderExamples_EqualsGetComponentExamplesAsyncOutput()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();
        var component = await indexer.GetComponentAsync("MudButton", TestContext.Current.CancellationToken);
        var examples = ComponentExampleTools.FilterExamples(component!.Examples, null);

        // Act & Assert
        Assert.Equal(
            await ComponentExampleTools.GetComponentExamplesAsync(
                indexer, NullLogger, _versionContext, "MudButton", 5, null, TestContext.Current.CancellationToken),
            ComponentExampleTools.RenderExamples("MudButton", examples, 5, "9.0.0"));
    }

    [Fact]
    public async Task RenderExample_EqualsGetExampleByNameAsyncOutput()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();
        var component = await indexer.GetComponentAsync("MudButton", TestContext.Current.CancellationToken);
        var example = ComponentExampleTools.FindExample(component!.Examples, "Basic Button");

        // Act & Assert
        Assert.Equal(
            await ComponentExampleTools.GetExampleByNameAsync(
                indexer, NullLogger, _versionContext, "MudButton", "Basic Button", TestContext.Current.CancellationToken),
            ComponentExampleTools.RenderExample("MudButton", example!, "9.0.0"));
    }

    [Fact]
    public async Task RenderExampleList_EqualsListComponentExamplesAsyncOutput()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();
        var component = await indexer.GetComponentAsync("MudButton", TestContext.Current.CancellationToken);

        // Act & Assert
        Assert.Equal(
            await ComponentExampleTools.ListComponentExamplesAsync(
                indexer, NullLogger, _versionContext, "MudButton", TestContext.Current.CancellationToken),
            ComponentExampleTools.RenderExampleList("MudButton", component!.Examples, "9.0.0"));
    }

    [Fact]
    public void FilterExamples_MatchesNameDescriptionOrFeature()
    {
        // Arrange - one match by Name, one by Description, one by Features, one non-matching
        IReadOnlyList<ComponentExample> list =
        [
            new ComponentExample("Widget Panel", "plain description", "<x/>", null, "a.razor", []),
            new ComponentExample("Alpha", "A widget demo", "<x/>", null, "b.razor", []),
            new ComponentExample("Beta", "nothing special", "<x/>", null, "c.razor", ["widget"]),
            new ComponentExample("Gamma", "no match here", "<x/>", null, "d.razor", ["other"])
        ];

        // Act
        var filtered = ComponentExampleTools.FilterExamples(list, "widget");
        var unfiltered = ComponentExampleTools.FilterExamples(list, null);

        // Assert
        Assert.Equal(3, filtered.Count);
        Assert.Contains(filtered, e => e.Name == "Widget Panel");
        Assert.Contains(filtered, e => e.Name == "Alpha");
        Assert.Contains(filtered, e => e.Name == "Beta");
        Assert.DoesNotContain(filtered, e => e.Name == "Gamma");
        Assert.Equal(list.Count, unfiltered.Count);
    }

    [Fact]
    public void FindExample_PrefersExactMatchOverContains()
    {
        // Arrange - "Icon Button" (contains) precedes the exact "Button" match
        IReadOnlyList<ComponentExample> list =
        [
            new ComponentExample("Icon Button", "has icon", "<x/>", null, "a.razor", []),
            new ComponentExample("Button", "plain", "<x/>", null, "b.razor", [])
        ];

        // Act
        var found = ComponentExampleTools.FindExample(list, "Button");

        // Assert
        Assert.Equal("Button", found!.Name);
    }

    #endregion

    #region Helper Methods

    private static IComponentIndexer CreateMockIndexerWithExamples()
    {
        var indexer = new Mock<IComponentIndexer>();

        var component = new ComponentInfo(
            Name: "MudButton",
            Namespace: "MudBlazor",
            Summary: "A Material Design button component",
            Description: "Use buttons for primary user actions.",
            Category: "Buttons",
            BaseType: "MudBaseButton",
            Parameters: [
                new ComponentParameter("Color", "Color", "The button color", "Color.Default", false, false, "Appearance"),
                new ComponentParameter("Variant", "Variant", "The button variant", "Variant.Text", false, false, "Appearance")
            ],
            Events: [
                new ComponentEvent("OnClick", "MouseEventArgs", "Callback when clicked")
            ],
            Methods: [],
            Examples: [
                new ComponentExample(
                    "Basic Button",
                    "A basic button example",
                    "<MudButton>Click Me</MudButton>",
                    null,
                    "BasicButtonExample.razor",
                    ["basic", "simple"]
                ),
                new ComponentExample(
                    "Icon Button",
                    "A button with an icon",
                    "<MudButton StartIcon=\"@Icons.Material.Filled.Add\">Add</MudButton>",
                    null,
                    "IconButtonExample.razor",
                    ["icon", "material"]
                ),
                new ComponentExample(
                    "Disabled Button",
                    "A disabled button",
                    "<MudButton Disabled=\"true\">Disabled</MudButton>",
                    null,
                    "DisabledButtonExample.razor",
                    ["disabled", "state"]
                )
            ],
            RelatedComponents: ["MudIconButton", "MudFab"],
            DocumentationUrl: "https://mudblazor.com/components/button",
            SourceUrl: "https://github.com/MudBlazor/MudBlazor"
        );

        indexer.Setup(x => x.GetComponentAsync("MudButton", It.IsAny<CancellationToken>()))
            .ReturnsAsync(component);
        indexer.Setup(x => x.GetComponentAsync("Button", It.IsAny<CancellationToken>()))
            .ReturnsAsync(component);

        return indexer.Object;
    }

    #endregion
}
