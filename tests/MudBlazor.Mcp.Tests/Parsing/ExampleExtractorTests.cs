// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging;
using MudBlazor.Mcp.Services.Parsing;

namespace MudBlazor.Mcp.Tests.Parsing;

public class ExampleExtractorTests
{
    private readonly ExampleExtractor _extractor;

    public ExampleExtractorTests()
    {
        var logger = Mock.Of<ILogger<ExampleExtractor>>();
        _extractor = new ExampleExtractor(logger);
    }

    [Fact]
    public async Task ParseExampleFileAsync_WithRazorExample_ExtractsMarkupAndCode()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".razor";
        var content = """
            @* Basic button example *@
            <MudButton Color="Color.Primary" Variant="Variant.Filled">
                Click Me
            </MudButton>

            @code {
                private void HandleClick()
                {
                    Console.WriteLine("Clicked!");
                }
            }
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _extractor.ParseExampleFileAsync(tempFile, "MudButton", CancellationToken.None);

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

    [Fact]
    public async Task ParseExampleFileAsync_WithNoCodeBlock_OnlyExtractsMarkup()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".razor";
        var content = """
            <MudButton Color="Color.Primary">
                Simple Button
            </MudButton>
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _extractor.ParseExampleFileAsync(tempFile, "MudButton", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("MudButton", result.RazorMarkup);
            Assert.Null(result.CSharpCode);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseExampleFileAsync_WithFeatures_ExtractsFeaturedFeatures()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".razor";
        var content = """
            <MudButton Color="Color.Primary" Variant="Variant.Filled" Size="Size.Large" @onclick="HandleClick">
                Click Me
            </MudButton>

            @code {
                private void HandleClick() { }
            }
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _extractor.ParseExampleFileAsync(tempFile, "MudButton", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Features);
            // Should detect common features like Colors, Variants, Sizes
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseExampleFileAsync_CleansUpDirectives()
    {
        // Arrange
        var tempFile = Path.GetTempFileName() + ".razor";
        var content = """
            @page "/components/button/basic"
            @using MudBlazor
            @namespace MudBlazor.Docs.Examples

            <MudButton>Test</MudButton>
            """;
        await File.WriteAllTextAsync(tempFile, content);

        try
        {
            // Act
            var result = await _extractor.ParseExampleFileAsync(tempFile, "MudButton", CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.DoesNotContain("@page", result.RazorMarkup);
            Assert.DoesNotContain("@using", result.RazorMarkup);
            Assert.DoesNotContain("@namespace", result.RazorMarkup);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task ParseExampleFileAsync_NonExistentFile_ReturnsNull()
    {
        // Act
        var result = await _extractor.ParseExampleFileAsync("/nonexistent/path.razor", "MudButton", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }
}
