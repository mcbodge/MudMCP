// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MudBlazor.Mcp.Models;

namespace MudBlazor.Mcp.Services.Parsing;

/// <summary>
/// Extracts code examples from MudBlazor documentation example files.
/// </summary>
public sealed partial class ExampleExtractor
{
    private readonly ILogger<ExampleExtractor> _logger;

    public ExampleExtractor(ILogger<ExampleExtractor> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts all examples for a component from the docs folder.
    /// </summary>
    /// <param name="docsPath">The path to the documentation folder.</param>
    /// <param name="componentName">The component name to extract examples for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of component examples.</returns>
    public async Task<List<ComponentExample>> ExtractExamplesAsync(
        string docsPath,
        string componentName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);

        var examples = new List<ComponentExample>();

        // Component examples are typically in: Docs/Pages/Components/{ComponentName}/{ComponentName}*Example.razor
        // The folder name is usually the component name without "Mud" prefix
        var folderName = componentName.StartsWith("Mud") ? componentName[3..] : componentName;
        var componentDocsPath = Path.Combine(docsPath, "Pages", "Components", folderName);

        if (!Directory.Exists(componentDocsPath))
        {
            _logger.LogDebug("No docs folder found for component {ComponentName} at {Path}", 
                componentName, componentDocsPath);
            return examples;
        }

        // Find all example files
        string[] exampleFiles;
        try
        {
            exampleFiles = Directory.GetFiles(componentDocsPath, "*Example*.razor", SearchOption.AllDirectories);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to docs folder: {Path}", componentDocsPath);
            return examples;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "IO error accessing docs folder: {Path}", componentDocsPath);
            return examples;
        }

        foreach (var filePath in exampleFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var example = await ParseExampleFileAsync(filePath, componentName, cancellationToken).ConfigureAwait(false);
                if (example is not null)
                {
                    examples.Add(example);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "IO error reading example file: {FilePath}", filePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied reading example file: {FilePath}", filePath);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to parse example file: {FilePath}", filePath);
            }
        }

        _logger.LogDebug("Found {Count} examples for {ComponentName}", examples.Count, componentName);
        return examples;
    }

    /// <summary>
    /// Parses a single example file.
    /// </summary>
    /// <param name="filePath">The path to the example file.</param>
    /// <param name="componentName">The component name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parsed component example, or null if the file doesn't exist.</returns>
    public async Task<ComponentExample?> ParseExampleFileAsync(
        string filePath,
        string componentName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);

        if (!File.Exists(filePath))
            return null;

        var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        var fileName = Path.GetFileNameWithoutExtension(filePath);

        // Extract example name from filename
        // Pattern: {ComponentName}{ExampleName}Example.razor -> ExampleName
        var exampleName = ExtractExampleName(fileName, componentName);

        // Extract description from comments or MudText
        var description = ExtractDescription(content);

        // Split into markup and code sections
        var (markup, code) = SplitMarkupAndCode(content);

        // Identify featured features/parameters
        var features = ExtractFeaturedFeatures(content);

        return new ComponentExample(
            Name: exampleName,
            Description: description,
            RazorMarkup: markup,
            CSharpCode: code,
            SourceFile: Path.GetFileName(filePath),
            Features: features
        );
    }

    private static string ExtractExampleName(string fileName, string componentName)
    {
        var baseName = componentName.StartsWith("Mud") ? componentName[3..] : componentName;
        
        // Try to extract the specific example name
        // Pattern: ButtonGroupExample -> "Group"
        // Pattern: ButtonIconAndLabelExample -> "IconAndLabel"
        
        if (fileName.StartsWith(baseName))
        {
            var remainder = fileName[baseName.Length..];
            if (remainder.EndsWith("Example"))
            {
                remainder = remainder[..^7];
            }
            
            // Convert PascalCase to readable text
            if (!string.IsNullOrEmpty(remainder))
            {
                return PascalCaseToSpaces(remainder);
            }
        }

        // Fallback: clean up the filename
        var name = fileName;
        if (name.EndsWith("Example"))
        {
            name = name[..^7];
        }
        return PascalCaseToSpaces(name);
    }

    private static string PascalCaseToSpaces(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return PascalCaseRegex().Replace(text, " $1").Trim();
    }

    private static string? ExtractDescription(string content)
    {
        // Check for description in comments at top of file
        var commentMatch = TopCommentRegex().Match(content);
        if (commentMatch.Success)
        {
            return commentMatch.Groups[1].Value.Trim();
        }

        // Check for description in @* *@ Razor comment
        var razorCommentMatch = RazorCommentRegex().Match(content);
        if (razorCommentMatch.Success)
        {
            return razorCommentMatch.Groups[1].Value.Trim();
        }

        return null;
    }

    private static (string? markup, string? code) SplitMarkupAndCode(string content)
    {
        // Split at @code block
        var codeMatch = CodeBlockRegex().Match(content);
        
        if (codeMatch.Success)
        {
            var markup = content[..codeMatch.Index].Trim();
            var code = codeMatch.Groups[1].Value.Trim();
            
            // Clean up the markup - remove @page directives, etc.
            markup = CleanMarkup(markup);
            
            return (markup, code);
        }

        // No @code block - it's all markup
        return (CleanMarkup(content), null);
    }

    private static string CleanMarkup(string markup)
    {
        // Remove @page directive
        markup = PageDirectiveRegex().Replace(markup, "");
        
        // Remove @using statements
        markup = UsingDirectiveRegex().Replace(markup, "");
        
        // Remove @inject statements
        markup = InjectDirectiveRegex().Replace(markup, "");
        
        // Remove @namespace
        markup = NamespaceDirectiveRegex().Replace(markup, "");
        
        // Clean up extra blank lines
        markup = MultipleNewlinesRegex().Replace(markup, "\n\n");
        
        return markup.Trim();
    }

    private static List<string> ExtractFeaturedFeatures(string content)
    {
        var features = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract parameter names used in the example
        var paramMatches = ParameterUsageRegex().Matches(content);
        foreach (Match match in paramMatches)
        {
            features.Add(match.Groups[1].Value);
        }

        // Extract commonly highlighted features
        if (content.Contains("@bind", StringComparison.OrdinalIgnoreCase))
            features.Add("Two-way binding");
        
        if (content.Contains("EventCallback") || content.Contains("@on"))
            features.Add("Event handling");
        
        if (content.Contains("Variant="))
            features.Add("Variants");
        
        if (content.Contains("Color="))
            features.Add("Colors");
        
        if (content.Contains("Size="))
            features.Add("Sizes");

        return features.Take(5).ToList();
    }

    [GeneratedRegex(@"(?<!^)([A-Z])")]
    private static partial Regex PascalCaseRegex();

    [GeneratedRegex(@"^//\s*(.+?)$", RegexOptions.Multiline)]
    private static partial Regex TopCommentRegex();

    [GeneratedRegex(@"^@\*\s*(.+?)\s*\*@", RegexOptions.Singleline)]
    private static partial Regex RazorCommentRegex();

    [GeneratedRegex(@"@code\s*\{(.*)\}", RegexOptions.Singleline)]
    private static partial Regex CodeBlockRegex();

    [GeneratedRegex(@"@page\s+""[^""]*""\s*\n?")]
    private static partial Regex PageDirectiveRegex();

    [GeneratedRegex(@"@using\s+[^\n]+\n?")]
    private static partial Regex UsingDirectiveRegex();

    [GeneratedRegex(@"@inject\s+[^\n]+\n?")]
    private static partial Regex InjectDirectiveRegex();

    [GeneratedRegex(@"@namespace\s+[^\n]+\n?")]
    private static partial Regex NamespaceDirectiveRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlinesRegex();

    [GeneratedRegex(@"(\w+)=""[^""]*""")]
    private static partial Regex ParameterUsageRegex();
}
