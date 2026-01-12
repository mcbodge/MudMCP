// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Tools;

/// <summary>
/// MCP tools for searching MudBlazor components.
/// </summary>
[McpServerToolType]
public sealed class ComponentSearchTools
{
    private static readonly string[] ValidSearchInOptions = ["name", "description", "parameters", "examples", "all"];
    private static readonly string[] ValidRelationshipTypes = ["all", "parent", "child", "sibling", "commonly_used_with"];

    /// <summary>
    /// Searches for MudBlazor components by query.
    /// </summary>
    [McpServerTool(Name = "search_components")]
    [Description("Searches MudBlazor components by name, description, or parameters. Returns components matching the query.")]
    public static async Task<string> SearchComponentsAsync(
        IComponentIndexer indexer,
        ILogger<ComponentSearchTools> logger,
        [Description("The search query (e.g., 'button', 'form input', 'date picker')")]
        string query,
        [Description("Fields to search in: 'name', 'description', 'parameters', 'examples', or 'all' (default)")]
        string? searchIn = null,
        [Description("Maximum number of results to return (default: 10, max: 50)")]
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(query, nameof(query));

        // Apply default values if not provided (MCP clients may send null for optional parameters)
        var effectiveSearchIn = searchIn ?? "all";
        var effectiveMaxResults = maxResults ?? 10;

        ToolValidation.RequireValidOption(effectiveSearchIn, ValidSearchInOptions, nameof(searchIn));
        ToolValidation.RequireInRange(effectiveMaxResults, 1, 50, nameof(maxResults));

        logger.LogDebug("Searching components with query: '{Query}', searchIn: {SearchIn}, maxResults: {MaxResults}",
            query, effectiveSearchIn, effectiveMaxResults);

        var searchFields = ParseSearchFields(effectiveSearchIn);
        var results = await indexer.SearchComponentsAsync(query, searchFields, effectiveMaxResults, cancellationToken);

        logger.LogDebug("Search returned {Count} results", results.Count);

        if (results.Count == 0)
        {
            return $"No components found matching '{query}'. Try a different query or use `list_components` to see all available components.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# Search Results for '{query}'");
        sb.AppendLine();
        sb.AppendLine($"Found {results.Count} component(s):");
        sb.AppendLine();

        foreach (var component in results)
        {
            sb.AppendLine($"## {component.Name}");
            sb.AppendLine();
            sb.AppendLine($"**Category:** {component.Category ?? "Uncategorized"}");
            sb.AppendLine();
            sb.AppendLine(component.Summary ?? "No description available.");
            sb.AppendLine();
            
            // Show matching parameters if searched
            if (searchFields.HasFlag(SearchFields.Parameters))
            {
                var matchingParams = component.Parameters
                    .Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                               (p.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true))
                    .Take(3)
                    .ToList();

                if (matchingParams.Count > 0)
                {
                    sb.AppendLine("**Matching Parameters:**");
                    foreach (var param in matchingParams)
                    {
                        sb.AppendLine($"- `{param.Name}` ({param.Type})");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        sb.AppendLine("*Use `get_component_detail` for comprehensive information about a specific component.*");

        return sb.ToString();
    }

    /// <summary>
    /// Gets all components in a specific category.
    /// </summary>
    [McpServerTool(Name = "get_components_by_category")]
    [Description("Gets all MudBlazor components in a specific category.")]
    public static async Task<string> GetComponentsByCategoryAsync(
        IComponentIndexer indexer,
        ILogger<ComponentSearchTools> logger,
        [Description("The category name (e.g., 'Buttons', 'Form Inputs & Controls', 'Navigation', 'Layout', 'Data Display', 'Feedback', 'Charts')")]
        string category,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(category, nameof(category));

        logger.LogDebug("Getting components by category: {Category}", category);

        var components = await indexer.GetComponentsByCategoryAsync(category, cancellationToken);

        if (components.Count == 0)
        {
            var categories = await indexer.GetCategoriesAsync(cancellationToken);
            logger.LogWarning("Category not found: {Category}. Available: {Available}",
                category, string.Join(", ", categories.Select(c => c.Name)));
            ToolValidation.ThrowCategoryNotFound(category, categories.Select(c => c.Name));
        }

        logger.LogDebug("Found {Count} components in category {Category}", components.Count, category);

        var sb = new StringBuilder();
        sb.AppendLine($"# {category} Components");
        sb.AppendLine();
        sb.AppendLine($"Found {components.Count} component(s):");
        sb.AppendLine();

        foreach (var component in components.OrderBy(c => c.Name))
        {
            sb.AppendLine($"### {component.Name}");
            sb.AppendLine();
            sb.AppendLine(component.Summary ?? "No description available.");
            sb.AppendLine();
            
            // Show key parameters
            var keyParams = component.Parameters.Take(5).ToList();
            if (keyParams.Count > 0)
            {
                sb.AppendLine("**Key Parameters:**");
                foreach (var param in keyParams)
                {
                    sb.AppendLine($"- `{param.Name}` ({param.Type})");
                }
                
                if (component.Parameters.Count > 5)
                {
                    sb.AppendLine($"- *... and {component.Parameters.Count - 5} more*");
                }
                sb.AppendLine();
            }

            if (component.Examples.Count > 0)
            {
                sb.AppendLine($"*{component.Examples.Count} example(s) available*");
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine("*Use `get_component_detail` for comprehensive information about a specific component.*");

        return sb.ToString();
    }

    /// <summary>
    /// Gets components related to a specific component.
    /// </summary>
    [McpServerTool(Name = "get_related_components")]
    [Description("Gets MudBlazor components related to a specific component through inheritance, category, or common usage.")]
    public static async Task<string> GetRelatedComponentsAsync(
        IComponentIndexer indexer,
        ILogger<ComponentSearchTools> logger,
        [Description("The component name (e.g., 'MudButton' or 'Button')")]
        string componentName,
        [Description("Type of relationship: 'all', 'parent', 'child', 'sibling', or 'commonly_used_with' (default: 'all')")]
        string? relationshipType = null,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(componentName, nameof(componentName));

        // Apply default value if not provided (MCP clients may send null for optional parameters)
        var effectiveRelationshipType = relationshipType ?? "all";
        ToolValidation.RequireValidOption(effectiveRelationshipType, ValidRelationshipTypes, nameof(relationshipType));

        logger.LogDebug("Getting related components for: {ComponentName}, relationship: {RelationshipType}",
            componentName, effectiveRelationshipType);

        var component = await indexer.GetComponentAsync(componentName, cancellationToken);
        
        if (component is null)
        {
            logger.LogWarning("Component not found: {ComponentName}", componentName);
            ToolValidation.ThrowComponentNotFound(componentName);
        }

        var relationship = ParseRelationshipType(effectiveRelationshipType);
        var related = await indexer.GetRelatedComponentsAsync(componentName, relationship, cancellationToken);

        logger.LogDebug("Found {Count} related components for {ComponentName}", related.Count, componentName);

        var sb = new StringBuilder();
        sb.AppendLine($"# Components Related to {component.Name}");
        sb.AppendLine();

        if (related.Count == 0)
        {
            sb.AppendLine("No related components found.");
            return sb.ToString();
        }

        // Group by relationship type
        var parent = component.BaseType is not null 
            ? related.FirstOrDefault(r => r.Name.Equals(component.BaseType, StringComparison.OrdinalIgnoreCase))
            : null;

        var children = related.Where(r => 
            r.BaseType?.Equals(component.Name, StringComparison.OrdinalIgnoreCase) == true).ToList();

        var siblings = related.Where(r => 
            r.Category == component.Category && 
            r.Name != component.Name &&
            (parent is null || r.Name != parent.Name) &&
            !children.Contains(r)).ToList();

        var others = related.Except(children).Except(siblings)
            .Where(r => parent is null || r.Name != parent.Name)
            .ToList();

        if (parent is not null)
        {
            sb.AppendLine("## Parent Component (Base Type)");
            sb.AppendLine();
            sb.AppendLine($"- **{parent.Name}**: {parent.Summary ?? "No description"}");
            sb.AppendLine();
        }

        if (children.Count > 0)
        {
            sb.AppendLine("## Child Components (Inherit from this)");
            sb.AppendLine();
            foreach (var child in children)
            {
                sb.AppendLine($"- **{child.Name}**: {child.Summary ?? "No description"}");
            }
            sb.AppendLine();
        }

        if (siblings.Count > 0)
        {
            sb.AppendLine($"## Same Category ({component.Category})");
            sb.AppendLine();
            foreach (var sibling in siblings.Take(10))
            {
                sb.AppendLine($"- **{sibling.Name}**: {sibling.Summary ?? "No description"}");
            }
            
            if (siblings.Count > 10)
            {
                sb.AppendLine($"- *... and {siblings.Count - 10} more*");
            }
            sb.AppendLine();
        }

        if (others.Count > 0)
        {
            sb.AppendLine("## Commonly Used Together");
            sb.AppendLine();
            foreach (var other in others)
            {
                sb.AppendLine($"- **{other.Name}**: {other.Summary ?? "No description"}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static SearchFields ParseSearchFields(string searchIn)
    {
        return searchIn.ToLowerInvariant() switch
        {
            "name" => SearchFields.Name,
            "description" => SearchFields.Description,
            "parameters" => SearchFields.Parameters,
            "examples" => SearchFields.Examples,
            _ => SearchFields.All
        };
    }

    private static RelationshipType ParseRelationshipType(string relationshipType)
    {
        return relationshipType.ToLowerInvariant().Replace("_", "") switch
        {
            "parent" => RelationshipType.Parent,
            "child" => RelationshipType.Child,
            "sibling" => RelationshipType.Sibling,
            "commonlyusedwith" => RelationshipType.CommonlyUsedWith,
            _ => RelationshipType.All
        };
    }
}
