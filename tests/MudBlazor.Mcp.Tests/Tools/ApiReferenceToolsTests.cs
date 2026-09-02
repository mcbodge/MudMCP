// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Tools;

namespace MudBlazor.Mcp.Tests.Tools;

public class ApiReferenceToolsTests
{
    private static readonly ILogger<ApiReferenceTools> NullLogger =
        NullLoggerFactory.Instance.CreateLogger<ApiReferenceTools>();

    private static readonly VersionContext _versionContext = new("9.0.0");

    #region GetEnumValuesAsync Tests

    [Fact]
    public async Task GetEnumValuesAsync_WithValidEnum_ReturnsValues()
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            indexer, NullLogger, _versionContext, "Color", TestContext.Current.CancellationToken);

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
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            indexer, NullLogger, _versionContext, "Size", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Size Enum Values", result);
        Assert.Contains("Small", result);
        Assert.Contains("Medium", result);
        Assert.Contains("Large", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithVariantEnum_ReturnsValues()
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            indexer, NullLogger, _versionContext, "Variant", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Text", result);
        Assert.Contains("Filled", result);
        Assert.Contains("Outlined", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_CaseInsensitive_ReturnsValues()
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act - use lowercase
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            indexer, NullLogger, _versionContext, "color", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Primary", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithEmptyEnumName_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ApiReferenceTools.GetEnumValuesAsync(indexer, NullLogger, _versionContext, "", TestContext.Current.CancellationToken));

        Assert.Contains("enumName", ex.Message);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithNullEnumName_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ApiReferenceTools.GetEnumValuesAsync(indexer, NullLogger, _versionContext, null!, TestContext.Current.CancellationToken));

        Assert.Contains("enumName", ex.Message);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithUnknownEnum_ThrowsMcpException()
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ApiReferenceTools.GetEnumValuesAsync(indexer, NullLogger, _versionContext, "UnknownEnum", TestContext.Current.CancellationToken));

        Assert.Contains("not found", ex.Message);
    }

    [Theory]
    [InlineData("AlignItems")]
    [InlineData("alignitems")]
    [InlineData("Justify")]
    [InlineData("justify")]
    public async Task GetEnumValuesAsync_WithLayoutEnums_ReturnsValues(string enumName)
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            indexer, NullLogger, _versionContext, enumName, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("Enum Values", result);
        Assert.Contains("Center", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_UsageExample_ShowsCorrectEnumSyntax()
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act - For any enum, the usage example should show EnumType.Value syntax
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            indexer, NullLogger, _versionContext, "AlignItems", TestContext.Current.CancellationToken);

        // Assert - Usage example must show the enum type prefix (e.g., AlignItems.Center)
        Assert.Contains("Usage Example", result);
        Assert.Contains("AlignItems.", result);
    }

    [Fact]
    public async Task GetEnumValuesAsync_WithExplicitValues_ShowsNumericColumn()
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act - the Severity enum defines explicit numeric values
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            indexer, NullLogger, _versionContext, "Severity", TestContext.Current.CancellationToken);

        // Assert - explicit numeric values render a Numeric column
        Assert.Contains("Numeric", result);
        Assert.Contains("Normal", result);
    }

    [Theory]
    [InlineData("Color", "Color.")]
    [InlineData("Size", "Size.")]
    [InlineData("Variant", "Variant.")]
    [InlineData("AlignItems", "AlignItems.")]
    [InlineData("Justify", "Justify.")]
    public async Task GetEnumValuesAsync_UsageExample_ShowsEnumTypePrefix(string enumName, string expectedPrefix)
    {
        // Arrange
        var indexer = CreateMockIndexerWithEnums();

        // Act
        var result = await ApiReferenceTools.GetEnumValuesAsync(
            indexer, NullLogger, _versionContext, enumName, TestContext.Current.CancellationToken);

        // Assert - Usage example must show the correct enum type prefix
        Assert.Contains(expectedPrefix, result);
    }

    private static readonly IReadOnlyDictionary<string, EnumInfo> SampleEnums =
        new Dictionary<string, EnumInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["Color"] = new EnumInfo("Color", "MudBlazor", "The color themes.",
            [
                new EnumValueInfo("Default", null, "The default theme color."),
                new EnumValueInfo("Primary", null, "The primary color."),
                new EnumValueInfo("Secondary", null, "The secondary color."),
                new EnumValueInfo("Success", null, "The success color."),
                new EnumValueInfo("Error", null, "The error color."),
            ]),
            ["Size"] = new EnumInfo("Size", "MudBlazor", null,
            [
                new EnumValueInfo("Small", null, "Small"),
                new EnumValueInfo("Medium", null, "Medium"),
                new EnumValueInfo("Large", null, "Large"),
            ]),
            ["Variant"] = new EnumInfo("Variant", "MudBlazor", null,
            [
                new EnumValueInfo("Text", null, "Text"),
                new EnumValueInfo("Filled", null, "Filled"),
                new EnumValueInfo("Outlined", null, "Outlined"),
            ]),
            ["AlignItems"] = new EnumInfo("AlignItems", "MudBlazor", null,
            [
                new EnumValueInfo("Baseline", null, "Baseline"),
                new EnumValueInfo("Center", null, "Center"),
                new EnumValueInfo("Start", null, "Start"),
                new EnumValueInfo("End", null, "End"),
                new EnumValueInfo("Stretch", null, "Stretch"),
            ]),
            ["Justify"] = new EnumInfo("Justify", "MudBlazor", null,
            [
                new EnumValueInfo("FlexStart", null, "FlexStart"),
                new EnumValueInfo("Center", null, "Center"),
                new EnumValueInfo("FlexEnd", null, "FlexEnd"),
            ]),
            ["Severity"] = new EnumInfo("Severity", "MudBlazor", null,
            [
                new EnumValueInfo("Normal", 0, "A normal message."),
                new EnumValueInfo("Info", 1, "An informational message."),
            ]),
        };

    private static IComponentIndexer CreateMockIndexerWithEnums()
    {
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.GetEnumAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string name, CancellationToken _) => SampleEnums.GetValueOrDefault(name));
        return indexer.Object;
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
            indexer, NullLogger, _versionContext, "MudButton", "all", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("API Reference", result);
    }

    [Fact]
    public async Task GetApiReferenceAsync_WithEnumName_FallsBackToEnumIndex()
    {
        // Arrange - not a component, but a known enum: should fall back to the enum index.
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.GetApiReferenceAsync("Color", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiReference?)null);
        indexer.Setup(x => x.GetEnumAsync("Color", It.IsAny<CancellationToken>()))
            .ReturnsAsync(SampleEnums["Color"]);

        // Act
        var result = await ApiReferenceTools.GetApiReferenceAsync(
            indexer.Object, NullLogger, _versionContext, "Color", "all", TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("Color Enum Values", result);
        Assert.Contains("Primary", result);
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
                indexer.Object, NullLogger, _versionContext, "Unknown", "all", TestContext.Current.CancellationToken));

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
                indexer.Object, NullLogger, _versionContext, "", "all", TestContext.Current.CancellationToken));

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
                indexer, NullLogger, _versionContext, "MudButton", "invalid", TestContext.Current.CancellationToken));

        Assert.Contains("memberType", ex.Message);
    }

    [Fact]
    public async Task GetApiReferenceAsync_FilterByProperties_ReturnsOnlyProperties()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ApiReferenceTools.GetApiReferenceAsync(
            indexer, NullLogger, _versionContext, "MudButton", "properties", TestContext.Current.CancellationToken);

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
