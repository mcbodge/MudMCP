// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Tools;

namespace MudBlazor.Mcp.Tests.Tools;

public class ApiReferenceToolsTests
{
    private static readonly ILogger<ApiReferenceTools> NullLogger =
        NullLoggerFactory.Instance.CreateLogger<ApiReferenceTools>();

    #region GetEnumValuesAsync Tests

    [Fact]
    public async Task GetEnumValuesAsync_WithValidEnum_ReturnsValues()
    {
        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            NullLogger, "Color", CancellationToken.None);

        // Assert
        Assert.Contains("Color Enum Values", result);
        Assert.Contains("Primary", result);
        Assert.Contains("Secondary", result);
        Assert.Contains("Success", result);
        Assert.Contains("Error", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithSizeEnum_ReturnsValues()
    {
        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            NullLogger, "Size", CancellationToken.None);

        // Assert
        Assert.Contains("Size Enum Values", result);
        Assert.Contains("Small", result);
        Assert.Contains("Medium", result);
        Assert.Contains("Large", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithVariantEnum_ReturnsValues()
    {
        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            NullLogger, "Variant", CancellationToken.None);

        // Assert
        Assert.Contains("Text", result);
        Assert.Contains("Filled", result);
        Assert.Contains("Outlined", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_CaseInsensitive_ReturnsValues()
    {
        // Act - use lowercase
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            NullLogger, "color", CancellationToken.None);

        // Assert
        Assert.Contains("Primary", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithEmptyEnumName_ThrowsMcpException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ApiReferenceTools.GetEnumValuesAsync(NullLogger, "", CancellationToken.None));

        Assert.Contains("enumName", ex.Message);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithNullEnumName_ThrowsMcpException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ApiReferenceTools.GetEnumValuesAsync(NullLogger, null!, CancellationToken.None));

        Assert.Contains("enumName", ex.Message);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithUnknownEnum_ThrowsMcpException()
    {
        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ApiReferenceTools.GetEnumValuesAsync(NullLogger, "UnknownEnum", CancellationToken.None));

        Assert.Contains("not found", ex.Message);
    }

    [Theory]
    [InlineData("AlignItems")]
    [InlineData("alignitems")]
    [InlineData("Justify")]
    [InlineData("justify")]
    public async Task GetEnumValuesAsync_WithLayoutEnums_ReturnsValues(string enumName)
    {
        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            NullLogger, enumName, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Enum Values", result);
        Assert.Contains("Center", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_UsageExample_ShowsCorrectEnumSyntax()
    {
        // Act - For any enum, the usage example should show EnumType.Value syntax
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            NullLogger, "AlignItems", CancellationToken.None);

        // Assert - Usage example must show the enum type prefix (e.g., AlignItems.Center)
        Assert.Contains("Usage Example", result);
        Assert.Contains("AlignItems.", result);
    }

    [Theory]
    [InlineData("Color", "Color.")]
    [InlineData("Size", "Size.")]
    [InlineData("Variant", "Variant.")]
    [InlineData("AlignItems", "AlignItems.")]
    [InlineData("Justify", "Justify.")]
    public async Task GetEnumValuesAsync_UsageExample_ShowsEnumTypePrefix(string enumName, string expectedPrefix)
    {
        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            NullLogger, enumName, CancellationToken.None);

        // Assert - Usage example must show the correct enum type prefix
        Assert.Contains(expectedPrefix, result);
    }

    #endregion

    #region GetApiReferenceAsync Tests

    [Fact]
    public async Task GetApiReferenceAsync_WithValidComponent_ReturnsReference()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ApiReferenceTools.GetApiReferenceAsync(
            indexer, NullLogger, "MudButton", "all", CancellationToken.None);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("API Reference", result);
    }

    [Fact]
    public async Task GetApiReferenceAsync_WithInvalidType_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.GetApiReferenceAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiReference?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ApiReferenceTools.GetApiReferenceAsync(
                indexer.Object, NullLogger, "Unknown", "all", CancellationToken.None));

        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public async Task GetApiReferenceAsync_WithEmptyTypeName_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ApiReferenceTools.GetApiReferenceAsync(
                indexer.Object, NullLogger, "", "all", CancellationToken.None));

        Assert.Contains("typeName", ex.Message);
    }

    [Fact]
    public async Task GetApiReferenceAsync_WithInvalidMemberType_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ApiReferenceTools.GetApiReferenceAsync(
                indexer.Object, NullLogger, "MudButton", "invalid", CancellationToken.None));

        Assert.Contains("memberType", ex.Message);
    }

    [Fact]
    public async Task GetApiReferenceAsync_FilterByProperties_ReturnsOnlyProperties()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ApiReferenceTools.GetApiReferenceAsync(
            indexer, NullLogger, "MudButton", "properties", CancellationToken.None);

        // Assert
        Assert.Contains("Properties", result);
        Assert.Contains("Color", result);
    }

    #endregion

    private static IComponentIndexer CreateMockIndexer()
    {
        var indexer = new Mock<IComponentIndexer>();

        var apiReference = new ApiReference(
            TypeName: "MudButton",
            Namespace: "MudBlazor",
            Summary: "A Material Design button component",
            BaseType: "MudBaseButton",
            Members: [
                new ApiMember("Color", "Property", "Color", "The button color"),
                new ApiMember("Variant", "Property", "Variant", "The button variant"),
                new ApiMember("OnClick", "Event", "EventCallback<MouseEventArgs>", "Click event"),
                new ApiMember("FocusAsync", "Method", "Task", "Focus the button")
            ]
        );

        indexer.Setup(x => x.GetApiReferenceAsync("MudButton", It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiReference);

        return indexer.Object;
    }
}
