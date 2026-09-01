// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Services.Parsing;

namespace MudBlazor.Mcp.Tests.Services;

public class ComponentIndexerTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // Directory may already have been deleted; ignore during cleanup.
            }
            catch (IOException)
            {
                // Best-effort cleanup: ignore transient IO issues when deleting temp directories.
            }
            catch (UnauthorizedAccessException)
            {
                // Ignore permission issues during cleanup of test temp directories.
            }
        }
    }

    private string CreateTempRepoDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"mudmcp-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private ComponentIndexer CreateIndexer(
        IGitRepositoryService? gitService = null,
        XmlDocParser? xmlParser = null,
        RazorDocParser? razorParser = null,
        ExampleExtractor? exampleExtractor = null,
        CategoryMapper? categoryMapper = null,
        string? dataPath = null)
    {
        var (indexer, _) = CreateIndexerWithContext(gitService, xmlParser, razorParser, exampleExtractor, categoryMapper, dataPath);
        return indexer;
    }

    private (ComponentIndexer Indexer, VersionContext Context) CreateIndexerWithContext(
        IGitRepositoryService? gitService = null,
        XmlDocParser? xmlParser = null,
        RazorDocParser? razorParser = null,
        ExampleExtractor? exampleExtractor = null,
        CategoryMapper? categoryMapper = null,
        string? dataPath = null,
        MudBlazorOptions? mudBlazorOptions = null)
    {
        gitService ??= Mock.Of<IGitRepositoryService>(s => 
            s.IsAvailable == true && 
            s.RepositoryPath == "/fake/repo");
        
        xmlParser ??= new XmlDocParser(Mock.Of<ILogger<XmlDocParser>>());
        razorParser ??= new RazorDocParser(Mock.Of<ILogger<RazorDocParser>>());
        exampleExtractor ??= new ExampleExtractor(Mock.Of<ILogger<ExampleExtractor>>());
        categoryMapper ??= new CategoryMapper(Mock.Of<ILogger<CategoryMapper>>());
        
        var options = Options.Create(mudBlazorOptions ?? new MudBlazorOptions());
        var logger = Mock.Of<ILogger<ComponentIndexer>>();

        // Use a temp data path so cached index files don't leak onto the real file system.
        var basePath = dataPath ?? Path.Combine(Path.GetTempPath(), $"mudmcp-test-{Guid.NewGuid():N}");
        _tempDirs.Add(basePath);
        var versionContext = new VersionContext($"0.0.0-test-{Guid.NewGuid():N}", basePath);

        var indexer = new ComponentIndexer(
            gitService,
            xmlParser,
            razorParser,
            exampleExtractor,
            categoryMapper,
            versionContext,
            options,
            logger);

        return (indexer, versionContext);
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
            indexer.GetAllComponentsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetComponentAsync_WhenNotIndexed_ThrowsInvalidOperationException()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            indexer.GetComponentAsync("MudButton", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetCategoriesAsync_WhenNotIndexed_ThrowsInvalidOperationException()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            indexer.GetCategoriesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchComponentsAsync_WhenNotIndexed_ThrowsInvalidOperationException()
    {
        // Arrange
        var indexer = CreateIndexer();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => 
            indexer.SearchComponentsAsync("button", cancellationToken: TestContext.Current.CancellationToken));
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
            indexer.BuildIndexAsync(TestContext.Current.CancellationToken));
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

    [Fact]
    public async Task BuildIndexAsync_WithMultipleRazorCsFiles_IndexesAllComponents()
    {
        // Arrange - create a temp repo with multiple .razor.cs files in one directory
        var repoPath = CreateTempRepoDir();
        var buttonDir = Path.Combine(repoPath, "src", "MudBlazor", "Components", "Button");
        Directory.CreateDirectory(buttonDir);

        await File.WriteAllTextAsync(Path.Combine(buttonDir, "MudButton.razor.cs"), """
            namespace MudBlazor;
            public partial class MudButton : MudBaseButton
            {
                [Parameter] public string? Label { get; set; }
            }
            """, TestContext.Current.CancellationToken);

        await File.WriteAllTextAsync(Path.Combine(buttonDir, "MudIconButton.razor.cs"), """
            namespace MudBlazor;
            public partial class MudIconButton : MudBaseButton
            {
                [Parameter] public string? Icon { get; set; }
            }
            """, TestContext.Current.CancellationToken);

        await File.WriteAllTextAsync(Path.Combine(buttonDir, "MudFab.razor.cs"), """
            namespace MudBlazor;
            public partial class MudFab : MudBaseButton
            {
                [Parameter] public string? StartIcon { get; set; }
            }
            """, TestContext.Current.CancellationToken);

        var gitService = new Mock<IGitRepositoryService>();
        gitService.Setup(g => g.IsAvailable).Returns(true);
        gitService.Setup(g => g.RepositoryPath).Returns(repoPath);
        gitService.Setup(g => g.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var indexer = CreateIndexer(gitService: gitService.Object);

        // Act
        await indexer.BuildIndexAsync(TestContext.Current.CancellationToken);

        // Assert - all three components should be indexed
        var all = await indexer.GetAllComponentsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, all.Count);
        Assert.Contains(all, c => c.Name == "MudButton");
        Assert.Contains(all, c => c.Name == "MudIconButton");
        Assert.Contains(all, c => c.Name == "MudFab");
    }

    [Fact]
    public async Task BuildIndexAsync_WithMixedRazorCsAndCsFiles_IndexesBothTypes()
    {
        // Arrange - .razor.cs takes precedence over .cs for same component,
        // but different components in .cs-only form should still be indexed
        var repoPath = CreateTempRepoDir();
        var gridDir = Path.Combine(repoPath, "src", "MudBlazor", "Components", "Grid");
        Directory.CreateDirectory(gridDir);

        await File.WriteAllTextAsync(Path.Combine(gridDir, "MudGrid.razor.cs"), """
            namespace MudBlazor;
            public partial class MudGrid : MudComponentBase
            {
                [Parameter] public int Spacing { get; set; }
            }
            """, TestContext.Current.CancellationToken);

        // This .cs file is for a DIFFERENT component (no corresponding .razor.cs)
        await File.WriteAllTextAsync(Path.Combine(gridDir, "MudItem.cs"), """
            namespace MudBlazor;
            public partial class MudItem : MudComponentBase
            {
                [Parameter] public int Xs { get; set; }
            }
            """, TestContext.Current.CancellationToken);

        // This .cs file has a corresponding .razor.cs above, so it should NOT be indexed separately
        await File.WriteAllTextAsync(Path.Combine(gridDir, "MudGrid.cs"), """
            namespace MudBlazor;
            public partial class MudGrid
            {
                public void SomeMethod() { }
            }
            """, TestContext.Current.CancellationToken);

        var gitService = new Mock<IGitRepositoryService>();
        gitService.Setup(g => g.IsAvailable).Returns(true);
        gitService.Setup(g => g.RepositoryPath).Returns(repoPath);
        gitService.Setup(g => g.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var indexer = CreateIndexer(gitService: gitService.Object);

        // Act
        await indexer.BuildIndexAsync(TestContext.Current.CancellationToken);

        // Assert - MudGrid from .razor.cs and MudItem from .cs
        var all = await indexer.GetAllComponentsAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, all.Count);
        Assert.Contains(all, c => c.Name == "MudGrid");
        Assert.Contains(all, c => c.Name == "MudItem");
    }

    [Fact]
    public async Task BuildIndexAsync_WithStaleCacheSchemaVersion_DeletesAndRebuilds()
    {
        // Arrange — create a repo with one component, and a stale index.json with wrong SchemaVersion
        var repoPath = CreateTempRepoDir();
        var buttonDir = Path.Combine(repoPath, "src", "MudBlazor", "Components", "Button");
        Directory.CreateDirectory(buttonDir);

        await File.WriteAllTextAsync(Path.Combine(buttonDir, "MudButton.razor.cs"), """
            namespace MudBlazor;
            public partial class MudButton : MudBaseButton
            {
                [Parameter] public string? Label { get; set; }
            }
            """, TestContext.Current.CancellationToken);

        var gitService = new Mock<IGitRepositoryService>();
        gitService.Setup(g => g.IsAvailable).Returns(true);
        gitService.Setup(g => g.RepositoryPath).Returns(repoPath);
        gitService.Setup(g => g.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (indexer, context) = CreateIndexerWithContext(gitService: gitService.Object);

        // Write a stale index.json with a wrong SchemaVersion (999)
        Directory.CreateDirectory(Path.GetDirectoryName(context.IndexPath)!);
        var staleJson = """
            {
                "SchemaVersion": 999,
                "IncludeInternalComponents": false,
                "IncludeDeprecatedComponents": true,
                "MaxExamplesPerComponent": 20,
                "Components": [],
                "ApiReferences": []
            }
            """;
        await File.WriteAllTextAsync(context.IndexPath, staleJson, TestContext.Current.CancellationToken);

        // Act — should detect stale schema, delete the file, and rebuild from repo
        await indexer.BuildIndexAsync(TestContext.Current.CancellationToken);

        // Assert — component was indexed from the repo (not the empty stale cache)
        var all = await indexer.GetAllComponentsAsync(TestContext.Current.CancellationToken);
        Assert.Single(all);
        Assert.Contains(all, c => c.Name == "MudButton");
    }

    [Fact]
    public async Task BuildIndexAsync_WithStaleCacheOptions_DeletesAndRebuilds()
    {
        // Arrange — create a repo with one component, and a stale index.json with mismatched options
        var repoPath = CreateTempRepoDir();
        var buttonDir = Path.Combine(repoPath, "src", "MudBlazor", "Components", "Button");
        Directory.CreateDirectory(buttonDir);

        await File.WriteAllTextAsync(Path.Combine(buttonDir, "MudButton.razor.cs"), """
            namespace MudBlazor;
            public partial class MudButton : MudBaseButton
            {
                [Parameter] public string? Label { get; set; }
            }
            """, TestContext.Current.CancellationToken);

        var gitService = new Mock<IGitRepositoryService>();
        gitService.Setup(g => g.IsAvailable).Returns(true);
        gitService.Setup(g => g.RepositoryPath).Returns(repoPath);
        gitService.Setup(g => g.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Default options: IncludeInternalComponents=false, IncludeDeprecatedComponents=true, MaxExamplesPerComponent=20
        var (indexer, context) = CreateIndexerWithContext(gitService: gitService.Object);

        // Write a stale index.json with correct SchemaVersion but mismatched IncludeInternalComponents
        Directory.CreateDirectory(Path.GetDirectoryName(context.IndexPath)!);
        var staleJson = """
            {
                "SchemaVersion": 2,
                "IncludeInternalComponents": true,
                "IncludeDeprecatedComponents": true,
                "MaxExamplesPerComponent": 20,
                "Components": [],
                "ApiReferences": []
            }
            """;
        await File.WriteAllTextAsync(context.IndexPath, staleJson, TestContext.Current.CancellationToken);

        // Act — should detect options mismatch, delete the stale file, and rebuild
        await indexer.BuildIndexAsync(TestContext.Current.CancellationToken);

        // Assert — component was indexed from the repo (not the empty stale cache)
        var all = await indexer.GetAllComponentsAsync(TestContext.Current.CancellationToken);
        Assert.Single(all);
        Assert.Contains(all, c => c.Name == "MudButton");
        // The stale file should have been deleted and replaced with a new one
        Assert.True(File.Exists(context.IndexPath));
    }

    [Fact]
    public async Task BuildIndexAsync_WithCorruptedCacheFile_DeletesAndRebuilds()
    {
        // Arrange — create a repo with one component, and a corrupted (non-JSON) index.json
        var repoPath = CreateTempRepoDir();
        var buttonDir = Path.Combine(repoPath, "src", "MudBlazor", "Components", "Button");
        Directory.CreateDirectory(buttonDir);

        await File.WriteAllTextAsync(Path.Combine(buttonDir, "MudButton.razor.cs"), """
            namespace MudBlazor;
            public partial class MudButton : MudBaseButton
            {
                [Parameter] public string? Label { get; set; }
            }
            """, TestContext.Current.CancellationToken);

        var gitService = new Mock<IGitRepositoryService>();
        gitService.Setup(g => g.IsAvailable).Returns(true);
        gitService.Setup(g => g.RepositoryPath).Returns(repoPath);
        gitService.Setup(g => g.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (indexer, context) = CreateIndexerWithContext(gitService: gitService.Object);

        // Write corrupt data to the index.json path
        Directory.CreateDirectory(Path.GetDirectoryName(context.IndexPath)!);
        await File.WriteAllTextAsync(context.IndexPath, "{{not valid json!!", TestContext.Current.CancellationToken);

        // Act — should detect corruption, delete the bad file, and rebuild from repo
        await indexer.BuildIndexAsync(TestContext.Current.CancellationToken);

        // Assert — component was indexed from the repo (not the corrupted cache)
        var all = await indexer.GetAllComponentsAsync(TestContext.Current.CancellationToken);
        Assert.Single(all);
        Assert.Contains(all, c => c.Name == "MudButton");
        // The corrupted file should have been deleted and replaced with a valid one
        Assert.True(File.Exists(context.IndexPath));
    }

    [Fact]
    public async Task BuildIndexAsync_SourceUrl_UsesVersionContextTag()
    {
        // Arrange — the SourceUrl should reference the version tag, not the hard-coded "dev" branch
        var repoPath = CreateTempRepoDir();
        var buttonDir = Path.Combine(repoPath, "src", "MudBlazor", "Components", "Button");
        Directory.CreateDirectory(buttonDir);

        await File.WriteAllTextAsync(Path.Combine(buttonDir, "MudButton.razor.cs"), """
            namespace MudBlazor;
            public partial class MudButton : MudBaseButton
            {
                [Parameter] public string? Label { get; set; }
            }
            """, TestContext.Current.CancellationToken);

        var gitService = new Mock<IGitRepositoryService>();
        gitService.Setup(g => g.IsAvailable).Returns(true);
        gitService.Setup(g => g.RepositoryPath).Returns(repoPath);
        gitService.Setup(g => g.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var (indexer, context) = CreateIndexerWithContext(gitService: gitService.Object);

        // Act
        await indexer.BuildIndexAsync(TestContext.Current.CancellationToken);

        // Assert — SourceUrl must contain the version tag, not "tree/dev"
        var component = await indexer.GetComponentAsync("MudButton", TestContext.Current.CancellationToken);
        Assert.NotNull(component);
        Assert.NotNull(component.SourceUrl);
        Assert.Contains($"/tree/{context.Tag}/", component.SourceUrl);
        Assert.DoesNotContain("/tree/dev/", component.SourceUrl);
        Assert.EndsWith("/Components/Button", component.SourceUrl);
    }
}
