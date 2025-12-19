// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using MudBlazor.Mcp.Services.Parsing;

namespace MudBlazor.Mcp.Tests.Parsing;

public class CategoryMapperTests
{
    private readonly CategoryMapper _mapper;

    public CategoryMapperTests()
    {
        var logger = Mock.Of<ILogger<CategoryMapper>>();
        _mapper = new CategoryMapper(logger);
    }

    [Fact]
    public async Task InitializeAsync_SetsUpCategories()
    {
        // Act
        await _mapper.InitializeAsync("/repo", CancellationToken.None);

        // Assert
        var categories = _mapper.GetCategories();
        Assert.NotEmpty(categories);
    }

    [Theory]
    [InlineData("MudButton", "Buttons")]
    [InlineData("MudIconButton", "Buttons")]
    [InlineData("MudTextField", "Form Inputs & Controls")]
    [InlineData("MudSelect", "Form Inputs & Controls")]
    [InlineData("MudNavMenu", "Navigation")]
    [InlineData("MudTable", "Data Display")]
    [InlineData("MudAlert", "Feedback")]
    [InlineData("MudCard", "Cards")]
    public async Task GetCategoryName_ReturnsCorrectCategory(string componentName, string expectedCategory)
    {
        // Arrange
        await _mapper.InitializeAsync("/repo", CancellationToken.None);

        // Act
        var category = _mapper.GetCategoryName(componentName);

        // Assert
        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public async Task GetCategoryForComponent_ReturnsFullCategoryInfo()
    {
        // Arrange
        await _mapper.InitializeAsync("/repo", CancellationToken.None);

        // Act
        var category = _mapper.GetCategoryForComponent("MudButton");

        // Assert
        Assert.NotNull(category);
        Assert.Equal("Buttons", category.Name);
        Assert.NotNull(category.Description);
        Assert.NotEmpty(category.Description);
        Assert.Contains("MudButton", category.ComponentNames);
    }

    [Fact]
    public async Task GetComponentsInCategory_ReturnsComponentsInCategory()
    {
        // Arrange
        await _mapper.InitializeAsync("/repo", CancellationToken.None);

        // Act
        var components = _mapper.GetComponentsInCategory("Buttons");

        // Assert
        Assert.NotEmpty(components);
        Assert.Contains("MudButton", components);
        Assert.Contains("MudIconButton", components);
    }

    [Theory]
    [InlineData("MudNewButton", "Buttons")]
    [InlineData("MudCustomTextField", "Form Inputs & Controls")]
    [InlineData("MudSpecialChart", "Charts")]
    [InlineData("MudCustomDialog", "Feedback")]
    public async Task InferCategoryFromName_InfersCorrectCategory(string componentName, string expectedCategory)
    {
        // Arrange
        await _mapper.InitializeAsync("/repo", CancellationToken.None);

        // Act
        var category = _mapper.InferCategoryFromName(componentName);

        // Assert
        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public async Task GetCategoryName_UnknownComponent_ReturnsNull()
    {
        // Arrange
        await _mapper.InitializeAsync("/repo", CancellationToken.None);

        // Act
        var category = _mapper.GetCategoryName("UnknownComponent");

        // Assert
        Assert.Null(category);
    }
}
