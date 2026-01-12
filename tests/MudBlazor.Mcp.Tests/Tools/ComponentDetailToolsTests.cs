// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Tools;

namespace MudBlazor.Mcp.Tests.Tools;

public class ComponentDetailToolsTests
{
    private static readonly ILogger<ComponentDetailTools> NullLogger = 
        NullLoggerFactory.Instance.CreateLogger<ComponentDetailTools>();

    [Fact]
    public async Task GetComponentDetailAsync_WithValidComponent_ReturnsDetails()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentDetailTools.GetComponentDetailAsync(
            indexer, NullLogger, "MudButton", false, true, CancellationToken.None);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("A Material Design button", result);
        Assert.Contains("Parameters", result);
        Assert.Contains("Color", result);
    }

    [Fact]
    public async Task GetComponentDetailAsync_WithInvalidComponent_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.GetComponentAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComponentInfo?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentDetailTools.GetComponentDetailAsync(
                indexer.Object, NullLogger, "Unknown", false, true, CancellationToken.None));

        Assert.Contains("not found", ex.Message);
        Assert.Contains("list_components", ex.Message);
    }

    [Fact]
    public async Task GetComponentDetailAsync_WithExamples_IncludesExamples()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentDetailTools.GetComponentDetailAsync(
            indexer, NullLogger, "MudButton", false, true, CancellationToken.None);

        // Assert
        Assert.Contains("Examples", result);
        Assert.Contains("Basic", result);
    }

    [Fact]
    public async Task GetComponentDetailAsync_WithNullOptionalParameters_UsesDefaults()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act - simulating what happens when MCP client doesn't send optional parameters
        var result = await ComponentDetailTools.GetComponentDetailAsync(
            indexer, NullLogger, "MudButton", null, null, CancellationToken.None);

        // Assert - default is includeExamples=true, so examples should be included
        Assert.Contains("MudButton", result);
        Assert.Contains("Examples", result);
    }

    [Fact]
    public async Task GetComponentParametersAsync_ReturnsParameters()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentDetailTools.GetComponentParametersAsync(
            indexer, NullLogger, "MudButton", null, CancellationToken.None);

        // Assert
        Assert.Contains("Color", result);
        Assert.Contains("Variant", result);
    }

    [Fact]
    public async Task GetComponentParametersAsync_WithBoolParameter_ShowsUsageHint()
    {
        // Arrange
        var indexer = CreateMockIndexerWithBoolParam();

        // Act
        var result = await ComponentDetailTools.GetComponentParametersAsync(
            indexer, NullLogger, "MudStack", null, CancellationToken.None);

        // Assert - Bool parameters should show usage hint with true/false
        Assert.Contains("Row", result);
        // Should indicate bool usage syntax: Row="true" or Row="false"
        Assert.Contains("\"true\"", result);
    }

    [Fact]
    public async Task GetComponentParametersAsync_WithEnumParameter_ShowsUsageHint()
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnumParam();

        // Act
        var result = await ComponentDetailTools.GetComponentParametersAsync(
            indexer, NullLogger, "MudStack", null, CancellationToken.None);

        // Assert - Enum parameters should show usage hint with enum type prefix
        Assert.Contains("AlignItems", result);
        // Should indicate enum usage syntax: AlignItems="AlignItems.Center"
        Assert.Contains("AlignItems.", result);
    }

    private static IComponentIndexer CreateMockIndexerWithBoolParam()
    {
        var indexer = new Mock<IComponentIndexer>();
        
        var component = new ComponentInfo(
            Name: "MudStack",
            Namespace: "MudBlazor",
            Summary: "A component for stacking items",
            Description: "Stack children vertically or horizontally.",
            Category: "Layout",
            BaseType: "MudComponentBase",
            Parameters: [
                new ComponentParameter("Row", "bool", "If true, items are stacked horizontally", "false", false, false, "Behavior")
            ],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null
        );

        indexer.Setup(x => x.GetComponentAsync("MudStack", It.IsAny<CancellationToken>()))
            .ReturnsAsync(component);

        return indexer.Object;
    }

    private static IComponentIndexer CreateMockIndexerWithEnumParam()
    {
        var indexer = new Mock<IComponentIndexer>();
        
        var component = new ComponentInfo(
            Name: "MudStack",
            Namespace: "MudBlazor",
            Summary: "A component for stacking items",
            Description: "Stack children vertically or horizontally.",
            Category: "Layout",
            BaseType: "MudComponentBase",
            Parameters: [
                new ComponentParameter("AlignItems", "AlignItems", "Defines the alignment of items", "AlignItems.Stretch", false, false, "Behavior")
            ],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null
        );

        indexer.Setup(x => x.GetComponentAsync("MudStack", It.IsAny<CancellationToken>()))
            .ReturnsAsync(component);

        return indexer.Object;
    }

    private static IComponentIndexer CreateMockIndexer()
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
                new ComponentParameter("Variant", "Variant", "The button variant", "Variant.Text", false, false, "Appearance"),
                new ComponentParameter("Disabled", "bool", "Whether the button is disabled", "false", false, false, "Behavior")
            ],
            Events: [
                new ComponentEvent("OnClick", "MouseEventArgs", "Callback when clicked")
            ],
            Methods: [
                new ComponentMethod("FocusAsync", "Task", "Focuses the button", [], true)
            ],
            Examples: [
                new ComponentExample("Basic", "Basic button usage", "<MudButton>Click</MudButton>", null, "BasicExample.razor", [])
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
}
