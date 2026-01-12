// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Tools;

namespace MudBlazor.Mcp.Tests.Tools;

public class ComponentListToolsTests
{
    private static readonly ILogger<ComponentListTools> NullLogger = 
        NullLoggerFactory.Instance.CreateLogger<ComponentListTools>();

    [Fact]
    public async Task ListComponentsAsync_WithNoFilter_ReturnsAllComponents()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentListTools.ListComponentsAsync(indexer, NullLogger, null, true, CancellationToken.None);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("MudTextField", result);
        Assert.Contains("2 total", result);
    }

    [Fact]
    public async Task ListComponentsAsync_WithNullIncludeDetails_UsesDefault()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act - simulating what happens when MCP client doesn't send includeDetails
        var result = await ComponentListTools.ListComponentsAsync(indexer, NullLogger, null, null, CancellationToken.None);

        // Assert - default is includeDetails=true, so details should be included
        Assert.Contains("MudButton", result);
        Assert.Contains("Parameters:", result); // This indicates details are included
    }

    [Fact]
    public async Task ListComponentsAsync_WithCategoryFilter_ReturnsFilteredComponents()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentListTools.ListComponentsAsync(indexer, NullLogger, "Buttons", true, CancellationToken.None);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.DoesNotContain("MudTextField", result);
    }

    [Fact]
    public async Task ListComponentsAsync_WithEmptyResults_ThrowsMcpException()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.IsIndexed).Returns(true);
        indexer.Setup(x => x.GetComponentsByCategoryAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        indexer.Setup(x => x.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ComponentCategory("Buttons", "Buttons", "Button components", ["MudButton"])]);

        // Act & Assert
        await Assert.ThrowsAsync<ModelContextProtocol.McpException>(async () =>
            await ComponentListTools.ListComponentsAsync(indexer.Object, NullLogger, "Unknown", true, CancellationToken.None));
    }

    [Fact]
    public async Task ListCategoriesAsync_ReturnsAllCategories()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentListTools.ListCategoriesAsync(indexer, NullLogger, CancellationToken.None);

        // Assert
        Assert.Contains("Buttons", result);
        Assert.Contains("Form Inputs", result);
    }

    private static IComponentIndexer CreateMockIndexer()
    {
        var indexer = new Mock<IComponentIndexer>();
        
        indexer.Setup(x => x.IsIndexed).Returns(true);
        
        var components = new List<ComponentInfo>
        {
            new("MudButton", "MudBlazor", "A button component", null, "Buttons", 
                null, [], [], [], [], [], null, null),
            new("MudTextField", "MudBlazor", "A text field component", null, "Form Inputs & Controls", 
                null, [], [], [], [], [], null, null)
        };

        var categories = new List<ComponentCategory>
        {
            new("Buttons", "Buttons", "Button components", ["MudButton"]),
            new("Form Inputs & Controls", "Form Inputs", "Form input components", ["MudTextField"])
        };

        indexer.Setup(x => x.GetAllComponentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(components);
        indexer.Setup(x => x.GetComponentsByCategoryAsync("Buttons", It.IsAny<CancellationToken>()))
            .ReturnsAsync(components.Where(c => c.Category == "Buttons").ToList());
        indexer.Setup(x => x.GetCategoriesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(categories);

        return indexer.Object;
    }
}
