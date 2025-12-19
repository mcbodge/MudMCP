// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Tools;

/// <summary>
/// MCP tools for getting component examples.
/// </summary>
[McpServerToolType]
public sealed class ComponentExampleTools
{
    /// <summary>
    /// Gets code examples for a MudBlazor component.
    /// </summary>
    [McpServerTool(Name = "get_component_examples")]
    [Description("Gets code examples for a specific MudBlazor component, showing how to use it in different scenarios.")]
    public static async Task<string> GetComponentExamplesAsync(
        IComponentIndexer indexer,
        ILogger<ComponentExampleTools> logger,
        [Description("The component name (e.g., 'MudButton' or 'Button')")]
        string componentName,
        [Description("Maximum number of examples to return (default: 5, max: 20)")]
        int maxExamples = 5,
        [Description("Optional filter for example names (e.g., 'basic', 'icon', 'disabled')")]
        string? filter = null,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(componentName, nameof(componentName));
        ToolValidation.RequireInRange(maxExamples, 1, 20, nameof(maxExamples));

        logger.LogDebug("Getting examples for component: {ComponentName}, maxExamples: {MaxExamples}, filter: {Filter}",
            componentName, maxExamples, filter ?? "none");

        var component = await indexer.GetComponentAsync(componentName, cancellationToken);
        
        if (component is null)
        {
            logger.LogWarning("Component not found: {ComponentName}", componentName);
            ToolValidation.ThrowComponentNotFound(componentName);
        }

        logger.LogDebug("Found {ExampleCount} examples for {ComponentName}", component.Examples.Count, componentName);

        var examples = component.Examples;
        
        // Apply filter if provided
        if (!string.IsNullOrWhiteSpace(filter))
        {
            examples = examples
                .Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                           (e.Description?.Contains(filter, StringComparison.OrdinalIgnoreCase) == true) ||
                           e.Features.Any(f => f.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        if (examples.Count == 0)
        {
            return filter is null 
                ? $"No examples available for {component.Name}."
                : $"No examples matching '{filter}' found for {component.Name}. Try without a filter to see all examples.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {component.Name} Examples");
        sb.AppendLine();
        sb.AppendLine($"*{examples.Count} example(s) available*");
        sb.AppendLine();

        var displayExamples = examples.Take(maxExamples).ToList();

        foreach (var example in displayExamples)
        {
            sb.AppendLine($"## {example.Name}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(example.Description))
            {
                sb.AppendLine(example.Description);
                sb.AppendLine();
            }

            if (example.Features.Count > 0)
            {
                sb.AppendLine($"**Features demonstrated:** {string.Join(", ", example.Features)}");
                sb.AppendLine();
            }

            // Razor markup
            if (!string.IsNullOrEmpty(example.RazorMarkup))
            {
                sb.AppendLine("### Razor Markup");
                sb.AppendLine();
                sb.AppendLine("```razor");
                sb.AppendLine(example.RazorMarkup.Trim());
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // C# code-behind
            if (!string.IsNullOrEmpty(example.CSharpCode))
            {
                sb.AppendLine("### Code-Behind");
                sb.AppendLine();
                sb.AppendLine("```csharp");
                sb.AppendLine(example.CSharpCode.Trim());
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // Source file reference
            if (!string.IsNullOrEmpty(example.SourceFile))
            {
                sb.AppendLine($"*Source: {example.SourceFile}*");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();
        }

        if (examples.Count > maxExamples)
        {
            sb.AppendLine($"*{examples.Count - maxExamples} more example(s) available. Increase `maxExamples` to see more.*");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets a specific example by name.
    /// </summary>
    [McpServerTool(Name = "get_example_by_name")]
    [Description("Gets a specific code example by its name from a MudBlazor component.")]
    public static async Task<string> GetExampleByNameAsync(
        IComponentIndexer indexer,
        ILogger<ComponentExampleTools> logger,
        [Description("The component name (e.g., 'MudButton' or 'Button')")]
        string componentName,
        [Description("The example name to find (e.g., 'Basic', 'Icon Button', 'Disabled')")]
        string exampleName,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(componentName, nameof(componentName));
        ToolValidation.RequireNonEmpty(exampleName, nameof(exampleName));

        logger.LogDebug("Getting example '{ExampleName}' for component: {ComponentName}",
            exampleName, componentName);

        var component = await indexer.GetComponentAsync(componentName, cancellationToken);
        
        if (component is null)
        {
            logger.LogWarning("Component not found: {ComponentName}", componentName);
            ToolValidation.ThrowComponentNotFound(componentName);
        }

        // Try to find the example (fuzzy match)
        var example = component.Examples.FirstOrDefault(e => 
            e.Name.Equals(exampleName, StringComparison.OrdinalIgnoreCase)) ??
            component.Examples.FirstOrDefault(e => 
            e.Name.Contains(exampleName, StringComparison.OrdinalIgnoreCase));

        if (example is null)
        {
            logger.LogWarning("Example not found: {ExampleName} for {ComponentName}", exampleName, componentName);
            ToolValidation.ThrowExampleNotFound(exampleName, componentName, component.Examples.Select(e => e.Name));
        }

        logger.LogDebug("Found example '{ExampleName}' for {ComponentName}", example.Name, componentName);

        var sb = new StringBuilder();
        sb.AppendLine($"# {component.Name} - {example.Name}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(example.Description))
        {
            sb.AppendLine(example.Description);
            sb.AppendLine();
        }

        if (example.Features.Count > 0)
        {
            sb.AppendLine($"**Features demonstrated:** {string.Join(", ", example.Features)}");
            sb.AppendLine();
        }

        // Full Razor markup
        if (!string.IsNullOrEmpty(example.RazorMarkup))
        {
            sb.AppendLine("## Razor Markup");
            sb.AppendLine();
            sb.AppendLine("```razor");
            sb.AppendLine(example.RazorMarkup.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Full C# code
        if (!string.IsNullOrEmpty(example.CSharpCode))
        {
            sb.AppendLine("## Code-Behind");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine(example.CSharpCode.Trim());
            sb.AppendLine("```");
            sb.AppendLine();
        }

        // Usage tips based on features
        if (example.Features.Count > 0)
        {
            sb.AppendLine("## Usage Tips");
            sb.AppendLine();
            GenerateUsageTips(sb, component.Name, example.Features);
        }

        sb.AppendLine($"*Source: {example.SourceFile}*");

        return sb.ToString();
    }

    /// <summary>
    /// Lists all example names for a component.
    /// </summary>
    [McpServerTool(Name = "list_component_examples")]
    [Description("Lists all available example names for a MudBlazor component without the full code.")]
    public static async Task<string> ListComponentExamplesAsync(
        IComponentIndexer indexer,
        ILogger<ComponentExampleTools> logger,
        [Description("The component name (e.g., 'MudButton' or 'Button')")]
        string componentName,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(componentName, nameof(componentName));

        logger.LogDebug("Listing examples for component: {ComponentName}", componentName);

        var component = await indexer.GetComponentAsync(componentName, cancellationToken);
        
        if (component is null)
        {
            logger.LogWarning("Component not found: {ComponentName}", componentName);
            ToolValidation.ThrowComponentNotFound(componentName);
        }

        logger.LogDebug("Found {Count} examples for {ComponentName}", component.Examples.Count, componentName);

        if (component.Examples.Count == 0)
        {
            return $"No examples available for {component.Name}.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {component.Name} Examples");
        sb.AppendLine();
        sb.AppendLine($"*{component.Examples.Count} example(s) available*");
        sb.AppendLine();
        sb.AppendLine("| Example Name | Features | Has Code-Behind |");
        sb.AppendLine("|--------------|----------|-----------------|");

        foreach (var example in component.Examples)
        {
            var features = example.Features.Count > 0 
                ? string.Join(", ", example.Features.Take(3)) + (example.Features.Count > 3 ? "..." : "")
                : "-";
            var hasCode = !string.IsNullOrEmpty(example.CSharpCode) ? "Yes" : "No";
            
            sb.AppendLine($"| {example.Name} | {features} | {hasCode} |");
        }

        sb.AppendLine();
        sb.AppendLine("*Use `get_example_by_name` to get the full code for a specific example.*");

        return sb.ToString();
    }

    private static void GenerateUsageTips(StringBuilder sb, string componentName, IReadOnlyList<string> features)
    {
        foreach (var feature in features)
        {
            switch (feature.ToLowerInvariant())
            {
                case "two-way binding":
                    sb.AppendLine("- **Two-way binding**: Use `@bind-Value` to automatically sync the component value with your model.");
                    break;
                case "event handling":
                    sb.AppendLine("- **Event handling**: Use event callbacks like `OnClick` to respond to user interactions.");
                    break;
                case "variants":
                    sb.AppendLine("- **Variants**: Available variants include `Filled`, `Outlined`, and `Text` for different visual styles.");
                    break;
                case "colors":
                    sb.AppendLine("- **Colors**: Use the `Color` parameter with values like `Primary`, `Secondary`, `Error`, `Warning`, `Success`, `Info`.");
                    break;
                case "sizes":
                    sb.AppendLine("- **Sizes**: Use the `Size` parameter with values like `Small`, `Medium`, `Large`.");
                    break;
            }
        }
        sb.AppendLine();
    }
}
