// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Services.Parsing;

namespace MudBlazor.Mcp.Tests.Services;

public class ComponentIndexerTests
{
    private static ComponentIndexer CreateIndexer(
        IGitRepositoryService? gitService = null,
        IDocumentationCache? cache = null,
        XmlDocParser? xmlParser = null,
        RazorDocParser? razorParser = null,
        ExampleExtractor? exampleExtractor = null,
        CategoryMapper? categoryMapper = null)
    {
        gitService ??= Mock.Of<IGitRepositoryService>(s => 
            s.IsAvailable == true && 
            s.RepositoryPath == "/fake/repo");
        
        cache ??= Mock.Of<IDocumentationCache>();
        xmlParser ??= new XmlDocParser(Mock.Of<ILogger<XmlDocParser>>());
        razorParser ??= new RazorDocParser(Mock.Of<ILogger<RazorDocParser>>());
        exampleExtractor ??= new ExampleExtractor(Mock.Of<ILogger<ExampleExtractor>>());
        categoryMapper ??= new CategoryMapper(Mock.Of<ILogger<CategoryMapper>>());
        
        var options = Options.Create(new MudBlazorOptions());
        var logger = Mock.Of<ILogger<ComponentIndexer>>();

        return new ComponentIndexer(
            gitService,
            cache,
            xmlParser,
            razorParser,
            exampleExtractor,
            categoryMapper,
            options,
            logger);
    }

    [Fact]
    public void IsIndexed_WhenNotBuilt_ReturnsFalse()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act & Assert
        Assert.False(indexer.IsIndexed);
    }

    [Fact]
    public void LastIndexed_WhenNotBuilt_ReturnsNull()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act & Assert
        Assert.Null(indexer.LastIndexed);
    }

    [Fact]
    public async Task GetAllComponentsAsync_WhenNotIndexed_ThrowsInvalidOperationException()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            indexer.GetAllComponentsAsync());
    }

    [Fact]
    public async Task GetComponentAsync_WhenNotIndexed_ThrowsInvalidOperationException()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            indexer.GetComponentAsync("MudButton"));
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenNotIndexed_ThrowsInvalidOperationException()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            indexer.GetCategoriesAsync());
    }

    [Fact]
    public async Task SearchComponentsAsync_WhenNotIndexed_ThrowsInvalidOperationException()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            indexer.SearchComponentsAsync("button"));
    }

    [Fact]
    public async Task BuildIndexAsync_WhenRepositoryNotAvailable_ThrowsInvalidOperationException()
    {
        // Arrange
        var gitService = new Mock<IGitRepositoryService>();
        gitService.Setup(g => g.IsAvailable).Returns(false);
        gitService.Setup(g => g.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        
        var indexer = CreateIndexer(gitService: gitService.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            indexer.BuildIndexAsync());
    }

    [Fact]
    public async Task BuildIndexAsync_CancellationToken_IsPropagated()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        var gitService = new Mock<IGitRepositoryService>();
        gitService.Setup(g => g.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        
        var indexer = CreateIndexer(gitService: gitService.Object);

        // Act & Assert - TaskCanceledException derives from OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
            indexer.BuildIndexAsync(cts.Token));
    }
}
