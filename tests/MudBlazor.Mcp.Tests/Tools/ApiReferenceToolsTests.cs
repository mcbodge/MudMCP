// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Tools;

namespace MudBlazor.Mcp.Tests.Tools;

public class ApiReferenceToolsTests
{
    private static readonly ILogger<ApiReferenceTools> NullLogger =
        NullLoggerFactory.Instance.CreateLogger<ApiReferenceTools>();

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
}
