// Copyright (c) 2024 MudBlazor.Mcp Contributors
// Licensed under the MIT License.

using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Tools;

namespace MudBlazor.Mcp.Tests.Tools;

public class ComponentListToolsTests
{
    [Fact]
    public async Task ListComponentsAsync_WithNoFilter_ReturnsAllComponents()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentListTools.ListComponentsAsync(indexer, null, true, CancellationToken.None);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("MudTextField", result);
        Assert.Contains("2 total", result);
    }

    [Fact]
    public async Task ListComponentsAsync_WithCategoryFilter_ReturnsFilteredComponents()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentListTools.ListComponentsAsync(indexer, "Buttons", true, CancellationToken.None);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.DoesNotContain("MudTextField", result);
    }

    [Fact]
    public async Task ListComponentsAsync_WithEmptyResults_ReturnsHelpfulMessage()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();
        indexer.Setup(x => x.GetComponentsByCategoryAsync("Unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await ComponentListTools.ListComponentsAsync(indexer.Object, "Unknown", true, CancellationToken.None);

        // Assert
        Assert.Contains("No components found", result);
    }

    [Fact]
    public async Task ListCategoriesAsync_ReturnsAllCategories()
    {
        // Arrange
        var indexer = CreateMockIndexer();

        // Act
        var result = await ComponentListTools.ListCategoriesAsync(indexer, CancellationToken.None);

        // Assert
        Assert.Contains("Buttons", result);
        Assert.Contains("Form Inputs", result);
    }

    private static IComponentIndexer CreateMockIndexer()
    {
        var indexer = new Mock<IComponentIndexer>();
        
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
