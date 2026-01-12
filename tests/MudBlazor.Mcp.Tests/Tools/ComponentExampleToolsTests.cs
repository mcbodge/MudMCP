// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Tools;

namespace MudBlazor.Mcp.Tests.Tools;

public class ComponentExampleToolsTests
{
    private static readonly ILogger<ComponentExampleTools> NullLogger =
        NullLoggerFactory.Instance.CreateLogger<ComponentExampleTools>();

    #region GetComponentExamplesAsync Tests

    [Fact]
    public async Task GetComponentExamplesAsync_WithValidComponent_ReturnsExamples()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act
        var result = await ComponentExampleTools.GetComponentExamplesAsync(
            indexer, NullLogger, "MudButton", 5, null, CancellationToken.None);

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
            indexer, NullLogger, "MudButton", null, null, CancellationToken.None);

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
            indexer, NullLogger, "MudButton", 5, "icon", CancellationToken.None);

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
                indexer.Object, NullLogger, "", 5, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetComponentExamplesAsync_WithNullComponentName_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();

        // Act & Assert
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentExampleTools.GetComponentExamplesAsync(
                indexer.Object, NullLogger, null!, 5, null, CancellationToken.None));
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
                indexer.Object, NullLogger, "Unknown", 5, null, CancellationToken.None));

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
                indexer, NullLogger, "MudButton", 0, null, CancellationToken.None));
    }

    [Fact]
    public async Task GetComponentExamplesAsync_WithMaxExamplesTooHigh_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexerWithExamples();

        // Act & Assert - maxExamples = 100 is out of range (max is 20)
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentExampleTools.GetComponentExamplesAsync(
                indexer, NullLogger, "MudButton", 100, null, CancellationToken.None));
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
            indexer, NullLogger, "MudButton", "Basic Button", CancellationToken.None);

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
            indexer, NullLogger, "MudButton", "Basic", CancellationToken.None);

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
                indexer, NullLogger, "MudButton", "NonExistent", CancellationToken.None));

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
            indexer, NullLogger, "MudButton", CancellationToken.None);

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
            indexer.Object, NullLogger, "MudEmpty", CancellationToken.None);

        // Assert
        Assert.Contains("No examples available", result);
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
