// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Tools;

/// <summary>
/// MCP tools for getting detailed component information.
/// </summary>
[McpServerToolType]
public sealed class ComponentDetailTools
{
    /// <summary>
    /// Gets detailed information about a specific MudBlazor component.
    /// </summary>
    [McpServerTool(Name = "get_component_detail")]
    [Description("Gets comprehensive details about a specific MudBlazor component including parameters, events, methods, and usage information.")]
    public static async Task<string> GetComponentDetailAsync(
        IComponentIndexer indexer,
        ILogger<ComponentDetailTools> logger,
        [Description("The component name (e.g., 'MudButton' or 'Button')")]
        string componentName,
        [Description("Include inherited members from base classes (default: false)")]
        bool includeInheritedMembers = false,
        [Description("Include code examples (default: true)")]
        bool includeExamples = true,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(componentName, nameof(componentName));

        logger.LogDebug("Getting component detail for: {ComponentName}, includeInherited: {IncludeInherited}, includeExamples: {IncludeExamples}",
            componentName, includeInheritedMembers, includeExamples);

        var component = await indexer.GetComponentAsync(componentName, cancellationToken);
        
        if (component is null)
        {
            logger.LogWarning("Component not found: {ComponentName}", componentName);
            ToolValidation.ThrowComponentNotFound(componentName);
        }

        logger.LogDebug("Found component {ComponentName} with {ParamCount} parameters, {EventCount} events, {ExampleCount} examples",
            component.Name, component.Parameters.Count, component.Events.Count, component.Examples.Count);

        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine($"# {component.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Namespace:** `{component.Namespace}`");
        
        if (!string.IsNullOrEmpty(component.Category))
        {
            sb.AppendLine($"**Category:** {component.Category}");
        }
        
        if (!string.IsNullOrEmpty(component.BaseType))
        {
            sb.AppendLine($"**Base Type:** `{component.BaseType}`");
        }
        
        sb.AppendLine();

        // Description
        sb.AppendLine("## Description");
        sb.AppendLine();
        sb.AppendLine(component.Summary ?? "No description available.");
        
        if (!string.IsNullOrEmpty(component.Description))
        {
            sb.AppendLine();
            sb.AppendLine(component.Description);
        }
        sb.AppendLine();

        // Parameters
        if (component.Parameters.Count > 0)
        {
            sb.AppendLine("## Parameters");
            sb.AppendLine();
            sb.AppendLine("| Parameter | Type | Description | Default |");
            sb.AppendLine("|-----------|------|-------------|---------|");
            
            foreach (var param in component.Parameters.OrderBy(p => p.Name))
            {
                var required = param.IsRequired ? " *(required)*" : "";
                var cascading = param.IsCascading ? " *(cascading)*" : "";
                var defaultVal = param.DefaultValue ?? "-";
                var desc = TruncateText(param.Description, 60);
                
                sb.AppendLine($"| `{param.Name}`{required}{cascading} | `{param.Type}` | {desc} | `{defaultVal}` |");
            }
            sb.AppendLine();
        }

        // Events
        if (component.Events.Count > 0)
        {
            sb.AppendLine("## Events");
            sb.AppendLine();
            sb.AppendLine("| Event | Type | Description |");
            sb.AppendLine("|-------|------|-------------|");
            
            foreach (var evt in component.Events.OrderBy(e => e.Name))
            {
                var eventType = evt.EventArgsType is not null 
                    ? $"EventCallback<{evt.EventArgsType}>" 
                    : "EventCallback";
                var desc = TruncateText(evt.Description, 80);
                
                sb.AppendLine($"| `{evt.Name}` | `{eventType}` | {desc} |");
            }
            sb.AppendLine();
        }

        // Methods
        if (component.Methods.Count > 0)
        {
            sb.AppendLine("## Public Methods");
            sb.AppendLine();
            
            foreach (var method in component.Methods.OrderBy(m => m.Name))
            {
                var asyncMarker = method.IsAsync ? "async " : "";
                var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));
                
                sb.AppendLine($"### `{asyncMarker}{method.ReturnType} {method.Name}({parameters})`");
                
                if (!string.IsNullOrEmpty(method.Description))
                {
                    sb.AppendLine();
                    sb.AppendLine(method.Description);
                }
                sb.AppendLine();
            }
        }

        // Examples
        if (includeExamples && component.Examples.Count > 0)
        {
            sb.AppendLine("## Examples");
            sb.AppendLine();
            
            // Show first 3 examples
            foreach (var example in component.Examples.Take(3))
            {
                sb.AppendLine($"### {example.Name}");
                
                if (!string.IsNullOrEmpty(example.Description))
                {
                    sb.AppendLine();
                    sb.AppendLine(example.Description);
                }
                sb.AppendLine();
                
                if (!string.IsNullOrEmpty(example.RazorMarkup))
                {
                    sb.AppendLine("```razor");
                    sb.AppendLine(example.RazorMarkup);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
                
                if (!string.IsNullOrEmpty(example.CSharpCode))
                {
                    sb.AppendLine("```csharp");
                    sb.AppendLine(example.CSharpCode);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            if (component.Examples.Count > 3)
            {
                sb.AppendLine($"*{component.Examples.Count - 3} more examples available. Use `get_component_examples` for all examples.*");
                sb.AppendLine();
            }
        }

        // Related Components
        if (component.RelatedComponents.Count > 0)
        {
            sb.AppendLine("## Related Components");
            sb.AppendLine();
            sb.AppendLine(string.Join(", ", component.RelatedComponents.Select(r => $"`{r}`")));
            sb.AppendLine();
        }

        // Links
        sb.AppendLine("## Links");
        sb.AppendLine();
        
        if (!string.IsNullOrEmpty(component.DocumentationUrl))
        {
            sb.AppendLine($"- [Documentation]({component.DocumentationUrl})");
        }
        
        if (!string.IsNullOrEmpty(component.SourceUrl))
        {
            sb.AppendLine($"- [Source Code]({component.SourceUrl})");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the parameters for a MudBlazor component.
    /// </summary>
    [McpServerTool(Name = "get_component_parameters")]
    [Description("Gets all parameters for a specific MudBlazor component, optionally filtered by category.")]
    public static async Task<string> GetComponentParametersAsync(
        IComponentIndexer indexer,
        ILogger<ComponentDetailTools> logger,
        [Description("The component name (e.g., 'MudButton' or 'Button')")]
        string componentName,
        [Description("Optional parameter category filter (e.g., 'Behavior', 'Appearance')")]
        string? parameterCategory = null,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(componentName, nameof(componentName));

        logger.LogDebug("Getting parameters for component: {ComponentName}, category filter: {Category}",
            componentName, parameterCategory ?? "none");

        var component = await indexer.GetComponentAsync(componentName, cancellationToken);
        
        if (component is null)
        {
            logger.LogWarning("Component not found: {ComponentName}", componentName);
            ToolValidation.ThrowComponentNotFound(componentName);
        }

        var parameters = parameterCategory is null 
            ? component.Parameters
            : component.Parameters.Where(p => 
                p.Category?.Equals(parameterCategory, StringComparison.OrdinalIgnoreCase) == true).ToList();

        if (parameters.Count == 0)
        {
            return parameterCategory is null 
                ? $"{component.Name} has no parameters."
                : $"{component.Name} has no parameters in category '{parameterCategory}'.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {component.Name} Parameters");
        sb.AppendLine();

        // Group by category if available
        var grouped = parameters
            .GroupBy(p => p.Category ?? "General")
            .OrderBy(g => g.Key);

        foreach (var group in grouped)
        {
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();

            foreach (var param in group.OrderBy(p => p.Name))
            {
                sb.AppendLine($"### `{param.Name}`");
                sb.AppendLine();
                sb.AppendLine($"- **Type:** `{param.Type}`");
                
                if (param.IsRequired)
                    sb.AppendLine("- **Required:** Yes");
                    
                if (param.IsCascading)
                    sb.AppendLine("- **Cascading:** Yes");
                    
                if (param.DefaultValue is not null)
                    sb.AppendLine($"- **Default:** `{param.DefaultValue}`");
                    
                if (!string.IsNullOrEmpty(param.Description))
                    sb.AppendLine($"- **Description:** {param.Description}");
                    
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return "-";

        text = text.Replace("\n", " ").Replace("\r", "");
        
        if (text.Length <= maxLength)
            return text;

        return text[..(maxLength - 3)] + "...";
    }
}
