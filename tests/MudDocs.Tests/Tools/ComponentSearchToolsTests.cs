// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudDocs.Configuration;
using MudDocs.Models;
using MudDocs.Services;
using MudDocs.Tools;

namespace MudDocs.Tests.Tools;

public class ComponentSearchToolsTests
{
    private static readonly ILogger<ComponentSearchTools> NullLogger =
        NullLoggerFactory.Instance.CreateLogger<ComponentSearchTools>();

    private static readonly VersionContext _versionContext = new("9.0.0");

    #region SearchComponentsAsync Tests

    [Fact]
    public async Task SearchComponentsAsync_WithValidQuery_ReturnsResults()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentSearchTools.SearchComponentsAsync(
            indexer, NullLogger, _versionContext, "button", "all", 10, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Search Results", result);
        Assert.Contains("MudButton", result);
    }

    [Fact]
    public async Task SearchComponentsAsync_WithNullSearchIn_UsesDefaultValue()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act - simulating what happens when MCP client doesn't send searchIn
        var result = await ComponentSearchTools.SearchComponentsAsync(
            indexer, NullLogger, _versionContext, "button", null, 10, TestContext.Current.CancellationToken);

        // Assert - should use default "all" and return results
        Assert.Contains("Search Results", result);
        Assert.Contains("MudButton", result);
    }

    [Fact]
    public async Task SearchComponentsAsync_WithNullMaxResults_UsesDefaultValue()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act - simulating what happens when MCP client doesn't send maxResults
        var result = await ComponentSearchTools.SearchComponentsAsync(
            indexer, NullLogger, _versionContext, "button", "all", null, TestContext.Current.CancellationToken);

        // Assert - should use default 10 and return results
        Assert.Contains("Search Results", result);
        Assert.Contains("MudButton", result);
    }

    [Fact]
    public async Task SearchComponentsAsync_WithAllNullOptionalParameters_UsesDefaults()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act - simulating what happens when MCP client sends null for all optional parameters
        var result = await ComponentSearchTools.SearchComponentsAsync(
            indexer, NullLogger, _versionContext, "button", null, null, TestContext.Current.CancellationToken);

        // Assert - should use defaults (searchIn="all", maxResults=10) and return results
        Assert.Contains("Search Results", result);
        Assert.Contains("MudButton", result);
    }

    [Fact]
    public async Task SearchComponentsAsync_WithEmptyQuery_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();

        // Act & Assert
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentSearchTools.SearchComponentsAsync(
                indexer.Object, NullLogger, _versionContext, "", null, null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchComponentsAsync_WithInvalidSearchIn_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act & Assert
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentSearchTools.SearchComponentsAsync(
                indexer, NullLogger, _versionContext, "button", "invalid_option", null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchComponentsAsync_WithMaxResultsOutOfRange_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act & Assert - maxResults = 0 is out of range (min is 1)
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentSearchTools.SearchComponentsAsync(
                indexer, NullLogger, _versionContext, "button", null, 0, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchComponentsAsync_WithNoResults_ReturnsNoResultsMessage()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.SearchComponentsAsync(
                It.IsAny<string>(),
                It.IsAny<SearchFields>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ComponentInfo>());

        // Act
        var result = await ComponentSearchTools.SearchComponentsAsync(
            indexer.Object, NullLogger, _versionContext, "nonexistent", null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("No components found", result);
    }

    [Fact]
    public async Task RenderSearchResults_EqualsSearchComponentsAsyncOutput()
    {
        // Arrange
        var indexer = CreateMockIndexer();
        var searchFields = ComponentSearchTools.ParseSearchFields("all");
        var results = await indexer.SearchComponentsAsync("button", searchFields, 10, TestContext.Current.CancellationToken);

        // Act & Assert - the extracted renderer reproduces the tool's Markdown byte-for-byte
        Assert.Equal(
            await ComponentSearchTools.SearchComponentsAsync(
                indexer, NullLogger, _versionContext, "button", "all", 10, TestContext.Current.CancellationToken),
            ComponentSearchTools.RenderSearchResults("button", results, searchFields, "9.0.0"));
    }

    #endregion

    #region GetComponentsByCategoryAsync Tests

    [Fact]
    public async Task GetComponentsByCategoryAsync_WithValidCategory_ReturnsComponents()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentSearchTools.GetComponentsByCategoryAsync(
            indexer, NullLogger, _versionContext, "Buttons", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Buttons", result);
        Assert.Contains("MudButton", result);
    }

    [Fact]
    public async Task GetComponentsByCategoryAsync_WithEmptyCategory_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();

        // Act & Assert
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentSearchTools.GetComponentsByCategoryAsync(
                indexer.Object, NullLogger, _versionContext, "", TestContext.Current.CancellationToken));
    }

    #endregion

    #region GetRelatedComponentsAsync Tests

    [Fact]
    public async Task GetRelatedComponentsAsync_WithValidComponent_ReturnsRelated()
    {
        // Arrange
        var indexer = CreateMockIndexerWithRelated();

        // Act
        var result = await ComponentSearchTools.GetRelatedComponentsAsync(
            indexer, NullLogger, _versionContext, "MudButton", "all", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Related to MudButton", result);
    }

    [Fact]
    public async Task GetRelatedComponentsAsync_WithNullRelationshipType_UsesDefaultValue()
    {
        // Arrange
        var indexer = CreateMockIndexerWithRelated();

        // Act - simulating what happens when MCP client doesn't send relationshipType
        var result = await ComponentSearchTools.GetRelatedComponentsAsync(
            indexer, NullLogger, _versionContext, "MudButton", null, TestContext.Current.CancellationToken);

        // Assert - should use default "all" and return results
        Assert.Contains("Related to MudButton", result);
    }

    [Fact]
    public async Task GetRelatedComponentsAsync_WithEmptyComponentName_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();

        // Act & Assert
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentSearchTools.GetRelatedComponentsAsync(
                indexer.Object, NullLogger, _versionContext, "", null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetRelatedComponentsAsync_WithInvalidRelationshipType_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexerWithRelated();

        // Act & Assert
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentSearchTools.GetRelatedComponentsAsync(
                indexer, NullLogger, _versionContext, "MudButton", "invalid_type", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetRelatedComponentsAsync_WithUnknownComponent_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.GetComponentAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ComponentInfo?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentSearchTools.GetRelatedComponentsAsync(
                indexer.Object, NullLogger, _versionContext, "Unknown", null, TestContext.Current.CancellationToken));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task RenderRelated_EqualsGetRelatedComponentsAsyncOutput()
    {
        // Arrange
        var indexer = CreateMockIndexerWithRelated();
        var component = await indexer.GetComponentAsync("MudButton", TestContext.Current.CancellationToken);
        var relationship = ComponentSearchTools.ParseRelationshipType("all");
        var related = await indexer.GetRelatedComponentsAsync("MudButton", relationship, TestContext.Current.CancellationToken);

        // Act & Assert - the extracted renderer reproduces the tool's Markdown byte-for-byte
        Assert.Equal(
            await ComponentSearchTools.GetRelatedComponentsAsync(
                indexer, NullLogger, _versionContext, "MudButton", "all", TestContext.Current.CancellationToken),
            ComponentSearchTools.RenderRelated(component!, related, "9.0.0"));
    }

    [Fact]
    public async Task GetRelatedComponentsAsync_WithNameWithoutMudPrefix_UsesResolvedName()
    {
        // Arrange - "Button" resolves to a component whose Name is "MudButton"
        var mock = new Mock<IComponentIndexer>();

        var buttonComponent = new ComponentInfo(
            Name: "MudButton",
            Namespace: "MudBlazor",
            Summary: "A Material Design button component",
            Description: "Use buttons for primary user actions.",
            Category: "Buttons",
            BaseType: "MudBaseButton",
            Parameters: [],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null);

        var iconButtonComponent = new ComponentInfo(
            Name: "MudIconButton",
            Namespace: "MudBlazor",
            Summary: "An icon button component",
            Description: "A button that displays an icon.",
            Category: "Buttons",
            BaseType: "MudBaseButton",
            Parameters: [],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null);

        mock.Setup(x => x.GetComponentAsync("Button", It.IsAny<CancellationToken>()))
            .ReturnsAsync(buttonComponent);
        mock.Setup(x => x.GetRelatedComponentsAsync("MudButton", It.IsAny<RelationshipType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([iconButtonComponent]);

        // Act - caller passes the raw "Button"
        var result = await ComponentSearchTools.GetRelatedComponentsAsync(
            mock.Object, NullLogger, _versionContext, "Button", "all", TestContext.Current.CancellationToken);

        // Assert - the resolved name "MudButton" (not raw "Button") drives the related lookup
        Assert.Contains("MudIconButton", result);
        mock.Verify(
            x => x.GetRelatedComponentsAsync("MudButton", It.IsAny<RelationshipType>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private static IComponentIndexer CreateMockIndexer()
    {
        var indexer = new Mock<IComponentIndexer>();

        var buttonComponent = new ComponentInfo(
            Name: "MudButton",
            Namespace: "MudBlazor",
            Summary: "A Material Design button component",
            Description: "Use buttons for primary user actions.",
            Category: "Buttons",
            BaseType: "MudBaseButton",
            Parameters: [
                new ComponentParameter("Color", "Color", "The button color", "Color.Default", false, false, "Appearance")
            ],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null
        );

        var components = new List<ComponentInfo> { buttonComponent };

        indexer.Setup(x => x.SearchComponentsAsync(
                It.IsAny<string>(),
                It.IsAny<SearchFields>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(components);

        indexer.Setup(x => x.GetComponentsByCategoryAsync("Buttons", It.IsAny<CancellationToken>()))
            .ReturnsAsync(components);

        indexer.Setup(x => x.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ComponentCategory("Buttons", "Buttons", "Button components", ["MudButton"])]);

        return indexer.Object;
    }

    private static IComponentIndexer CreateMockIndexerWithRelated()
    {
        var indexer = new Mock<IComponentIndexer>();

        var buttonComponent = new ComponentInfo(
            Name: "MudButton",
            Namespace: "MudBlazor",
            Summary: "A Material Design button component",
            Description: "Use buttons for primary user actions.",
            Category: "Buttons",
            BaseType: "MudBaseButton",
            Parameters: [],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: ["MudIconButton", "MudFab"],
            DocumentationUrl: null,
            SourceUrl: null
        );

        var iconButtonComponent = new ComponentInfo(
            Name: "MudIconButton",
            Namespace: "MudBlazor",
            Summary: "An icon button component",
            Description: "A button that displays an icon.",
            Category: "Buttons",
            BaseType: "MudBaseButton",
            Parameters: [],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: ["MudButton"],
            DocumentationUrl: null,
            SourceUrl: null
        );

        indexer.Setup(x => x.GetComponentAsync("MudButton", It.IsAny<CancellationToken>()))
            .ReturnsAsync(buttonComponent);

        indexer.Setup(x => x.GetRelatedComponentsAsync(
                "MudButton",
                It.IsAny<RelationshipType>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([iconButtonComponent]);

        return indexer.Object;
    }

    #endregion
}
