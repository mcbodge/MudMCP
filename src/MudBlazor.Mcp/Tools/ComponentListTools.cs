// Copyright (c) 2024 MudBlazor.Mcp Contributors
// Licensed under the MIT License.

using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Tools;

/// <summary>
/// MCP tools for listing and browsing MudBlazor components.
/// </summary>
[McpServerToolType]
public sealed class ComponentListTools
{
    /// <summary>
    /// Lists all available MudBlazor components with their categories.
    /// </summary>
    /// <param name="indexer">The component indexer service.</param>
    /// <param name="category">Optional category to filter by.</param>
    /// <param name="includeDetails">Whether to include parameter counts and descriptions.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Formatted list of components.</returns>
    [McpServerTool(Name = "list_components")]
    [Description("Lists all available MudBlazor components. Optionally filter by category and include additional details.")]
    public static async Task<string> ListComponentsAsync(
        IComponentIndexer indexer,
        [Description("Optional category to filter by (e.g., 'Buttons', 'Form Inputs', 'Navigation')")] 
        string? category = null,
        [Description("Include parameter counts and brief descriptions (default: true)")]
        bool includeDetails = true,
        CancellationToken cancellationToken = default)
    {
        var components = string.IsNullOrWhiteSpace(category)
            ? await indexer.GetAllComponentsAsync(cancellationToken)
            : await indexer.GetComponentsByCategoryAsync(category, cancellationToken);

        if (components.Count == 0)
        {
            return category is null 
                ? "No components found. The index may not have been built yet."
                : $"No components found in category '{category}'. Use list_components without a category to see all available components.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# MudBlazor Components ({components.Count} total)");
        sb.AppendLine();

        if (category is not null)
        {
            sb.AppendLine($"**Category:** {category}");
            sb.AppendLine();
        }

        // Group by category if not filtering
        var grouped = category is null 
            ? components.GroupBy(c => c.Category ?? "Uncategorized").OrderBy(g => g.Key)
            : components.GroupBy(c => category).OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();

            foreach (var component in group.OrderBy(c => c.Name))
            {
                if (includeDetails)
                {
                    sb.AppendLine($"- **{component.Name}**: {TruncateText(component.Summary, 80)}");
                    sb.AppendLine($"  - Parameters: {component.Parameters.Count}, Events: {component.Events.Count}, Examples: {component.Examples.Count}");
                }
                else
                {
                    sb.AppendLine($"- {component.Name}");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("*Use `get_component_detail` for detailed information about a specific component.*");

        return sb.ToString();
    }

    /// <summary>
    /// Lists all component categories with their descriptions and component counts.
    /// </summary>
    [McpServerTool(Name = "list_categories")]
    [Description("Lists all MudBlazor component categories with descriptions and component counts.")]
    public static async Task<string> ListCategoriesAsync(
        IComponentIndexer indexer,
        CancellationToken cancellationToken = default)
    {
        var categories = await indexer.GetCategoriesAsync(cancellationToken);
        var allComponents = await indexer.GetAllComponentsAsync(cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("# MudBlazor Component Categories");
        sb.AppendLine();

        foreach (var category in categories)
        {
            var componentCount = allComponents.Count(c => c.Category == category.Name);
            
            sb.AppendLine($"## {category.Title}");
            if (!string.IsNullOrEmpty(category.Description))
            {
                sb.AppendLine($"*{category.Description}*");
            }
            sb.AppendLine($"- **Components:** {componentCount}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine("*Use `list_components` with a category filter to see components in a specific category.*");

        return sb.ToString();
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "No description available";

        if (text.Length <= maxLength)
            return text;

        return text[..(maxLength - 3)] + "...";
    }
}
