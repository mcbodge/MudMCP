# Testing Guide

Comprehensive guide to testing the Mud MCP server.

## Table of Contents

- [Testing Philosophy](#testing-philosophy)
- [Test Framework](#test-framework)
- [Project Structure](#project-structure)
- [Running Tests](#running-tests)
- [Unit Testing Tools](#unit-testing-tools)
- [Unit Testing Services](#unit-testing-services)
- [Unit Testing Parsers](#unit-testing-parsers)
- [Mocking Patterns](#mocking-patterns)
- [Test Data Builders](#test-data-builders)
- [Best Practices](#best-practices)

---

## Testing Philosophy

Mud MCP follows these testing principles:

1. **Isolated Unit Tests**: Each test validates a single behavior
2. **Mocking Dependencies**: Use Moq to isolate components
3. **Descriptive Names**: Test names describe the scenario and expected outcome
4. **Arrange-Act-Assert**: Clear three-part structure
5. **Error Path Coverage**: Test both success and failure scenarios

---

## Test Framework

| Package | Purpose |
|---------|---------|
| **xUnit** | Test framework and assertions |
| **Moq** | Mocking framework for interfaces |
| **Microsoft.Extensions.Logging.Abstractions** | `NullLogger<T>` for testing |

### Dependencies

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" />
<PackageReference Include="xunit" />
<PackageReference Include="xunit.runner.visualstudio" />
<PackageReference Include="Moq" />
<PackageReference Include="coverlet.collector" />
```

---

## Project Structure

```
tests/
└── MudBlazor.Mcp.Tests/
    ├── MudBlazor.Mcp.Tests.csproj
    ├── Tools/
    │   ├── ComponentListToolsTests.cs
    │   ├── ComponentDetailToolsTests.cs
    │   ├── ComponentSearchToolsTests.cs
    │   ├── ComponentExampleToolsTests.cs
    │   └── ApiReferenceToolsTests.cs
    ├── Services/
    │   ├── ComponentIndexerTests.cs
    │   └── DocumentationCacheTests.cs
    └── Parsing/
        ├── XmlDocParserTests.cs
        ├── RazorDocParserTests.cs
        ├── ExampleExtractorTests.cs
        └── CategoryMapperTests.cs
```

---

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test

# Run with verbosity
dotnet test --verbosity normal

# Run specific test project
dotnet test tests/MudBlazor.Mcp.Tests

# Run specific test class
dotnet test --filter "FullyQualifiedName~ComponentListToolsTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~ListComponentsAsync_ReturnsAllComponents"

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Visual Studio / Rider

1. Open Test Explorer
2. Build solution
3. Run tests or run with coverage

### Watch Mode

```bash
dotnet watch test --project tests/MudBlazor.Mcp.Tests
```

---

## Unit Testing Tools

Tools use static methods with injected dependencies, making them easy to test.

### Basic Tool Test Pattern

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Tools;
using Moq;
using Xunit;

public class ComponentListToolsTests
{
    // Create a null logger instance for testing
    private static readonly ILogger<ComponentListTools> NullLogger = 
        NullLoggerFactory.Instance.CreateLogger<ComponentListTools>();

    [Fact]
    public async Task ListComponentsAsync_ReturnsAllComponents()
    {
        // Arrange
        var indexer = new Mock<IComponentIndexer>();
        var components = new List<ComponentInfo>
        {
            CreateTestComponent("MudButton"),
            CreateTestComponent("MudTextField"),
            CreateTestComponent("MudCard")
        };
        indexer.Setup(x => x.GetAllComponentsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(components);

        // Act
        var result = await ComponentListTools.ListComponentsAsync(
            indexer.Object,
            NullLogger,
            CancellationToken.None);

        // Assert
        Assert.Contains("MudButton", result);
        Assert.Contains("MudTextField", result);
        Assert.Contains("MudCard", result);
    }

    private static ComponentInfo CreateTestComponent(string name)
    {
        return new ComponentInfo(
            Name: name,
            Namespace: "MudBlazor",
            Summary: $"{name} component",
            Description: null,
            Category: "Buttons",
            BaseType: null,
            Parameters: [],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null
        );
    }
}
```

### Testing Error Scenarios

```csharp
[Fact]
public async Task GetComponentDetailAsync_WithInvalidComponent_ThrowsMcpException()
{
    // Arrange
    var indexer = new Mock<IComponentIndexer>();
    indexer.Setup(x => x.GetComponentAsync("Unknown", It.IsAny<CancellationToken>()))
        .ReturnsAsync((ComponentInfo?)null);

    // Act & Assert
    var ex = await Assert.ThrowsAsync<McpException>(async () =>
        await ComponentDetailTools.GetComponentDetailAsync(
            indexer.Object,
            NullLogger,
            "Unknown",
            includeInherited: false,
            includeExamples: true,
            CancellationToken.None));

    Assert.Contains("not found", ex.Message);
    Assert.Contains("list_components", ex.Message); // Helpful hint
}
```

### Testing Parameter Validation

```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
public async Task GetComponentDetailAsync_WithEmptyName_ThrowsMcpException(string? componentName)
{
    // Arrange
    var indexer = new Mock<IComponentIndexer>();

    // Act & Assert
    await Assert.ThrowsAsync<McpException>(async () =>
        await ComponentDetailTools.GetComponentDetailAsync(
            indexer.Object,
            NullLogger,
            componentName!,
            includeInherited: false,
            includeExamples: true,
            CancellationToken.None));
}
```

### Testing with Examples

```csharp
[Fact]
public async Task GetComponentDetailAsync_WithExamples_IncludesExamples()
{
    // Arrange
    var indexer = new Mock<IComponentIndexer>();
    var component = new ComponentInfo(
        Name: "MudButton",
        Namespace: "MudBlazor",
        Summary: "Button component",
        Description: null,
        Category: "Buttons",
        BaseType: null,
        Parameters: [
            new ComponentParameter("Color", "Color", "The color", null, false, false, null)
        ],
        Events: [],
        Methods: [],
        Examples: [
            new ComponentExample("Basic", "Basic usage", "<MudButton>Click</MudButton>", 
                null, "BasicExample.razor", [])
        ],
        RelatedComponents: [],
        DocumentationUrl: null,
        SourceUrl: null
    );

    indexer.Setup(x => x.GetComponentAsync("MudButton", It.IsAny<CancellationToken>()))
        .ReturnsAsync(component);

    // Act
    var result = await ComponentDetailTools.GetComponentDetailAsync(
        indexer.Object,
        NullLogger,
        "MudButton",
        includeInherited: false,
        includeExamples: true,
        CancellationToken.None);

    // Assert
    Assert.Contains("Examples", result);
    Assert.Contains("Basic", result);
    Assert.Contains("<MudButton>Click</MudButton>", result);
}
```

---

## Unit Testing Services

### Testing ComponentIndexer

```csharp
public class ComponentIndexerTests
{
    [Fact]
    public async Task BuildIndexAsync_WithValidRepository_IndexesComponents()
    {
        // Arrange
        var gitService = new Mock<IGitRepositoryService>();
        gitService.Setup(x => x.IsAvailable).Returns(true);
        gitService.Setup(x => x.RepositoryPath).Returns("./test-repo");
        gitService.Setup(x => x.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var cache = new Mock<IDocumentationCache>();
        var xmlParser = new XmlDocParser(NullLogger<XmlDocParser>.Instance);
        var razorParser = new RazorDocParser(NullLogger<RazorDocParser>.Instance);
        var exampleExtractor = new ExampleExtractor(NullLogger<ExampleExtractor>.Instance);
        var categoryMapper = new CategoryMapper(NullLogger<CategoryMapper>.Instance);
        var options = Options.Create(new MudBlazorOptions());
        var logger = NullLogger<ComponentIndexer>.Instance;

        var indexer = new ComponentIndexer(
            gitService.Object,
            cache.Object,
            xmlParser,
            razorParser,
            exampleExtractor,
            categoryMapper,
            options,
            logger);

        // Act & Assert
        // This test would need an actual repo structure or mocked file system
        // Consider using in-memory file abstractions for thorough testing
    }

    [Fact]
    public async Task GetComponentAsync_WithMudPrefix_ReturnsComponent()
    {
        // Arrange - setup indexer with pre-populated components
        var indexer = await CreateIndexerWithComponents(
            CreateTestComponent("MudButton"));

        // Act
        var result = await indexer.GetComponentAsync("MudButton");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MudButton", result.Name);
    }

    [Fact]
    public async Task GetComponentAsync_WithoutMudPrefix_ReturnsComponent()
    {
        // Arrange
        var indexer = await CreateIndexerWithComponents(
            CreateTestComponent("MudButton"));

        // Act
        var result = await indexer.GetComponentAsync("Button"); // Without "Mud"

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MudButton", result.Name);
    }
}
```

### Testing DocumentationCache

```csharp
public class DocumentationCacheTests
{
    [Fact]
    public async Task GetOrCreateAsync_WithCachedValue_ReturnsCached()
    {
        // Arrange
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new CacheOptions
        {
            ComponentCacheDurationMinutes = 30
        });
        var logger = NullLogger<DocumentationCache>.Instance;
        var cache = new DocumentationCache(memoryCache, options, logger);

        var component = CreateTestComponent("MudButton");
        await cache.SetComponentAsync("MudButton", component);

        // Act
        var result = await cache.GetComponentAsync("MudButton");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MudButton", result.Name);
    }

    [Fact]
    public async Task GetComponentAsync_WithExpiredCache_ReturnsNull()
    {
        // Arrange with very short expiration for testing
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new CacheOptions
        {
            ComponentCacheDurationMinutes = 0,
            SlidingExpirationMinutes = 0,
            AbsoluteExpirationMinutes = 0
        });
        var logger = NullLogger<DocumentationCache>.Instance;
        var cache = new DocumentationCache(memoryCache, options, logger);

        // Act
        var result = await cache.GetComponentAsync("MudButton");

        // Assert
        Assert.Null(result);
    }
}
```

---

## Unit Testing Parsers

### Testing XmlDocParser

```csharp
public class XmlDocParserTests
{
    private readonly XmlDocParser _parser;

    public XmlDocParserTests()
    {
        _parser = new XmlDocParser(NullLogger<XmlDocParser>.Instance);
    }

    [Fact]
    public void ParseSourceCode_ExtractsClassName()
    {
        // Arrange
        var sourceCode = @"
namespace MudBlazor
{
    public class MudButton : MudBaseButton
    {
        [Parameter]
        public Color Color { get; set; }
    }
}";

        // Act
        var result = _parser.ParseSourceCode(sourceCode, "MudButton.cs");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("MudButton", result.ClassName);
        Assert.Equal("MudBlazor", result.Namespace);
        Assert.Equal("MudBaseButton", result.BaseType);
    }

    [Fact]
    public void ParseSourceCode_ExtractsParameters()
    {
        // Arrange
        var sourceCode = @"
namespace MudBlazor
{
    public class MudButton
    {
        /// <summary>
        /// The button color.
        /// </summary>
        [Parameter]
        public Color Color { get; set; } = Color.Default;

        [Parameter]
        [EditorRequired]
        public string Label { get; set; }
    }
}";

        // Act
        var result = _parser.ParseSourceCode(sourceCode, "MudButton.cs");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Parameters.Count);
        
        var colorParam = result.Parameters.First(p => p.Name == "Color");
        Assert.Equal("Color", colorParam.Type);
        Assert.Contains("button color", colorParam.Description?.ToLower());
        
        var labelParam = result.Parameters.First(p => p.Name == "Label");
        Assert.True(labelParam.IsRequired);
    }

    [Fact]
    public void ParseSourceCode_ExtractsEvents()
    {
        // Arrange
        var sourceCode = @"
namespace MudBlazor
{
    public class MudButton
    {
        /// <summary>
        /// Callback when clicked.
        /// </summary>
        [Parameter]
        public EventCallback<MouseEventArgs> OnClick { get; set; }
    }
}";

        // Act
        var result = _parser.ParseSourceCode(sourceCode, "MudButton.cs");

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Events);
        Assert.Equal("OnClick", result.Events[0].Name);
        Assert.Equal("MouseEventArgs", result.Events[0].EventArgsType);
    }
}
```

### Testing ExampleExtractor

```csharp
public class ExampleExtractorTests
{
    private readonly ExampleExtractor _extractor;

    public ExampleExtractorTests()
    {
        _extractor = new ExampleExtractor(NullLogger<ExampleExtractor>.Instance);
    }

    [Fact]
    public void ParseExample_SplitsMarkupAndCode()
    {
        // Arrange
        var content = @"
<MudButton OnClick=""HandleClick"">Click Me</MudButton>

@code {
    private void HandleClick()
    {
        // Handle click
    }
}";
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, content);

        try
        {
            // Act
            var result = await _extractor.ParseExampleFileAsync(
                tempFile, "MudButton", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("MudButton", result.RazorMarkup);
            Assert.Contains("HandleClick", result.CSharpCode);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
```

### Testing CategoryMapper

```csharp
public class CategoryMapperTests
{
    private readonly CategoryMapper _mapper;

    public CategoryMapperTests()
    {
        _mapper = new CategoryMapper(NullLogger<CategoryMapper>.Instance);
    }

    [Theory]
    [InlineData("MudButton", "Buttons")]
    [InlineData("MudTextField", "Form Inputs & Controls")]
    [InlineData("MudNavMenu", "Navigation")]
    [InlineData("MudCard", "Cards")]
    [InlineData("MudAlert", "Feedback")]
    public async Task GetCategoryName_ReturnsCorrectCategory(string componentName, string expectedCategory)
    {
        // Arrange
        await _mapper.InitializeAsync("./repo", CancellationToken.None);

        // Act
        var category = _mapper.GetCategoryName(componentName);

        // Assert
        Assert.Equal(expectedCategory, category);
    }

    [Fact]
    public void InferCategoryFromName_InfersFromPattern()
    {
        // Arrange
        var unknownComponent = "MudCustomButton";

        // Act
        var category = _mapper.InferCategoryFromName(unknownComponent);

        // Assert
        Assert.Equal("Buttons", category); // Contains "button"
    }
}
```

---

## Mocking Patterns

### Mocking IComponentIndexer

```csharp
private static Mock<IComponentIndexer> CreateMockIndexer(params ComponentInfo[] components)
{
    var mock = new Mock<IComponentIndexer>();
    
    mock.Setup(x => x.IsIndexed).Returns(true);
    mock.Setup(x => x.GetAllComponentsAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(components.ToList());
    
    foreach (var component in components)
    {
        mock.Setup(x => x.GetComponentAsync(component.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(component);
        
        // Also setup without Mud prefix
        var shortName = component.Name.StartsWith("Mud") ? component.Name[3..] : component.Name;
        mock.Setup(x => x.GetComponentAsync(shortName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(component);
    }

    return mock;
}
```

### Mocking IGitRepositoryService

```csharp
private static Mock<IGitRepositoryService> CreateMockGitService(string repoPath, bool isAvailable = true)
{
    var mock = new Mock<IGitRepositoryService>();
    
    mock.Setup(x => x.IsAvailable).Returns(isAvailable);
    mock.Setup(x => x.RepositoryPath).Returns(repoPath);
    mock.Setup(x => x.CurrentCommitHash).Returns("abc1234");
    mock.Setup(x => x.EnsureRepositoryAsync(It.IsAny<CancellationToken>()))
        .ReturnsAsync(false);
    mock.Setup(x => x.GetPath(It.IsAny<string>()))
        .Returns<string>(path => Path.Combine(repoPath, path));

    return mock;
}
```

---

## Test Data Builders

### ComponentInfo Builder

```csharp
public class ComponentInfoBuilder
{
    private string _name = "MudButton";
    private string _namespace = "MudBlazor";
    private string _summary = "Test component";
    private string? _description;
    private string? _category = "Buttons";
    private List<ComponentParameter> _parameters = [];
    private List<ComponentEvent> _events = [];
    private List<ComponentMethod> _methods = [];
    private List<ComponentExample> _examples = [];

    public ComponentInfoBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ComponentInfoBuilder WithCategory(string category)
    {
        _category = category;
        return this;
    }

    public ComponentInfoBuilder WithParameter(string name, string type, string? description = null)
    {
        _parameters.Add(new ComponentParameter(name, type, description, null, false, false, null));
        return this;
    }

    public ComponentInfoBuilder WithEvent(string name, string? argsType = null)
    {
        _events.Add(new ComponentEvent(name, argsType, null));
        return this;
    }

    public ComponentInfoBuilder WithExample(string name, string markup)
    {
        _examples.Add(new ComponentExample(name, null, markup, null, null, []));
        return this;
    }

    public ComponentInfo Build() => new(
        Name: _name,
        Namespace: _namespace,
        Summary: _summary,
        Description: _description,
        Category: _category,
        BaseType: null,
        Parameters: _parameters,
        Events: _events,
        Methods: _methods,
        Examples: _examples,
        RelatedComponents: [],
        DocumentationUrl: null,
        SourceUrl: null
    );
}
```

### Usage

```csharp
[Fact]
public async Task Test_WithBuilder()
{
    var component = new ComponentInfoBuilder()
        .WithName("MudTextField")
        .WithCategory("Form Inputs")
        .WithParameter("Value", "string", "The input value")
        .WithParameter("Label", "string", "The label")
        .WithEvent("ValueChanged", "string")
        .WithExample("Basic", "<MudTextField />")
        .Build();

    // Use in test...
}
```

---

## Best Practices

### 1. One Assert Per Test (Ideally)

```csharp
// ❌ Multiple unrelated assertions
[Fact]
public async Task Test_Bad()
{
    var result = await GetComponent();
    Assert.NotNull(result);
    Assert.Equal("MudButton", result.Name);
    Assert.Equal("Buttons", result.Category);
    Assert.Equal(5, result.Parameters.Count);
}

// ✅ Focused assertions
[Fact]
public async Task GetComponent_ReturnsComponent() => Assert.NotNull(await GetComponent());

[Fact]
public async Task GetComponent_HasCorrectName() => Assert.Equal("MudButton", (await GetComponent()).Name);
```

### 2. Descriptive Test Names

```csharp
// ❌ Vague names
[Fact]
public async Task TestButton() { }

// ✅ Descriptive names: Method_Scenario_ExpectedResult
[Fact]
public async Task GetComponentAsync_WithValidName_ReturnsComponent() { }

[Fact]
public async Task GetComponentAsync_WithUnknownName_ReturnsNull() { }

[Fact]
public async Task ListComponentsAsync_WhenIndexIsEmpty_ReturnsEmptyList() { }
```

### 3. Use Theory for Multiple Inputs

```csharp
// ❌ Duplicate tests
[Fact]
public async Task Test_Button() => await TestComponent("MudButton");
[Fact]
public async Task Test_TextField() => await TestComponent("MudTextField");

// ✅ Use Theory
[Theory]
[InlineData("MudButton")]
[InlineData("MudTextField")]
[InlineData("MudCard")]
public async Task GetComponent_WithValidName_ReturnsComponent(string name)
{
    var result = await _indexer.GetComponentAsync(name);
    Assert.NotNull(result);
}
```

### 4. Arrange-Act-Assert

```csharp
[Fact]
public async Task GetComponentDetailAsync_WithValidComponent_ReturnsFormattedMarkdown()
{
    // Arrange
    var indexer = CreateMockIndexer(CreateTestComponent("MudButton"));
    
    // Act
    var result = await ComponentDetailTools.GetComponentDetailAsync(
        indexer, NullLogger, "MudButton", false, true, CancellationToken.None);

    // Assert
    Assert.Contains("# MudButton", result);
    Assert.Contains("Parameters", result);
}
```

### 5. Test Edge Cases

```csharp
[Theory]
[InlineData(null)]
[InlineData("")]
[InlineData("   ")]
[InlineData("\t\n")]
public async Task GetComponent_WithInvalidInput_ThrowsMcpException(string? input) { }

[Fact]
public async Task Search_WithSpecialCharacters_HandlesGracefully()
{
    var result = await _tools.SearchComponentsAsync(indexer, logger, 
        "button<script>", CancellationToken.None);
    Assert.NotNull(result);
}
```

---

## Next Steps

- [MCP Inspector Testing](./08-mcp-inspector.md) — Integration testing with MCP Inspector
- [IDE Integration](./09-ide-integration.md) — Configure in VS Code and Claude Desktop
- [Troubleshooting](./10-troubleshooting.md) — Common test failures and fixes
