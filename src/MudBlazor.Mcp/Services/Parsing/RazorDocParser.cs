// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MudBlazor.Mcp.Models;

namespace MudBlazor.Mcp.Services.Parsing;

/// <summary>
/// Parses Razor documentation files to extract component descriptions and sections.
/// </summary>
public sealed partial class RazorDocParser
{
    private readonly ILogger<RazorDocParser> _logger;

    public RazorDocParser(ILogger<RazorDocParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a Razor documentation file for component documentation.
    /// </summary>
    /// <param name="filePath">The path to the Razor documentation file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parse result, or null if the file doesn't exist or parsing failed.</returns>
    public async Task<RazorDocResult?> ParseDocumentationFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("Documentation file not found: {FilePath}", filePath);
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return ParseDocumentation(content, filePath);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading documentation file: {FilePath}", filePath);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied reading documentation file: {FilePath}", filePath);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to parse documentation file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Parses Razor documentation content.
    /// </summary>
    /// <param name="content">The Razor file content.</param>
    /// <param name="filePath">The file path for reference.</param>
    /// <returns>The parsed documentation result.</returns>
    public RazorDocResult? ParseDocumentation(string content, string filePath)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var componentName = ExtractComponentName(filePath);
        var description = ExtractSubTitle(content);
        var relatedComponents = ExtractRelatedComponents(content);

        return new RazorDocResult
        {
            FilePath = filePath,
            ComponentName = componentName,
            Description = description,
            RelatedComponents = relatedComponents
        };
    }

    private static string? ExtractComponentName(string filePath)
    {
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        
        // Handle pattern like "ButtonPage.razor" -> "MudButton"
        if (fileName.EndsWith("Page"))
        {
            var name = fileName[..^4]; // Remove "Page"
            return $"Mud{name}";
        }

        return null;
    }

    private static string? ExtractSubTitle(string content)
    {
        // Only use the explicit DocsPageHeader SubTitle. A generic <MudText> fallback grabbed
        // unrelated page text (e.g. a "Note" callout or a nav label), so when no SubTitle is
        // present we return null and let the authoritative source XML summary/remarks stand.
        var match = SubTitleAttributeRegex().Match(content);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static List<string> ExtractRelatedComponents(string content)
    {
        var related = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Find links to other components
        var linkMatches = ComponentLinkRegex().Matches(content);
        foreach (Match match in linkMatches)
        {
            var componentPath = match.Groups[1].Value;
            var componentName = Path.GetFileNameWithoutExtension(componentPath);
            
            if (componentName.EndsWith("Page"))
            {
                var name = $"Mud{componentName[..^4]}";
                related.Add(name);
            }
        }

        // Find MudXxx references in code
        var mudMatches = MudComponentRefRegex().Matches(content);
        foreach (Match match in mudMatches)
        {
            related.Add(match.Groups[1].Value);
        }

        return related.ToList();
    }

    [GeneratedRegex(@"SubTitle\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex SubTitleAttributeRegex();

    [GeneratedRegex(@"href\s*=\s*""/components/([^""]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex ComponentLinkRegex();

    [GeneratedRegex(@"<(Mud[A-Z][a-zA-Z]+)")]
    private static partial Regex MudComponentRefRegex();
}

/// <summary>
/// Result of parsing a Razor documentation file.
/// </summary>
public record RazorDocResult
{
    public required string FilePath { get; init; }
    public string? ComponentName { get; init; }
    public string? Description { get; init; }
    public List<string> RelatedComponents { get; init; } = [];
}
