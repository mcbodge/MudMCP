// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Tools;

/// <summary>
/// MCP tools for API reference documentation.
/// </summary>
[McpServerToolType]
public sealed class ApiReferenceTools
{
    private static readonly string[] ValidMemberTypes = ["all", "properties", "methods", "events"];

    /// <summary>
    /// Gets the API reference for a MudBlazor type.
    /// </summary>
    [McpServerTool(Name = "get_api_reference")]
    [Description("Gets the full API reference for a MudBlazor component or type, including all properties, methods, and events. Results are for the configured MudBlazor version. If a component seems missing, verify the --version matches your project's MudBlazor PackageReference in the .csproj file.")]
    public static async Task<string> GetApiReferenceAsync(
        IComponentIndexer indexer,
        ILogger<ApiReferenceTools> logger,
        VersionContext versionContext,
        [Description("The type name (e.g., 'MudButton', 'Color', 'Size')")]
        string typeName,
        [Description("Filter to specific member type: 'all', 'properties', 'methods', 'events' (default: 'all')")]
        string? memberType = null,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(typeName, nameof(typeName));

        // Apply default value if not provided (MCP clients may send null for optional parameters)
        var effectiveMemberType = memberType ?? "all";
        ToolValidation.RequireValidOption(effectiveMemberType, ValidMemberTypes, nameof(memberType));

        logger.LogDebug("Getting API reference for type: {TypeName}, memberType: {MemberType}",
            typeName, effectiveMemberType);

        var apiRef = await indexer.GetApiReferenceAsync(typeName, cancellationToken);
        
        if (apiRef is null)
        {
            // A component/type miss may still be an enum — fall back to the enum index before failing.
            var enumFallback = await indexer.GetEnumAsync(typeName, cancellationToken);
            if (enumFallback is not null)
            {
                logger.LogDebug("Type {TypeName} resolved as an enum via fallback", typeName);
                return RenderEnum(enumFallback, versionContext);
            }

            logger.LogWarning("Type not found: {TypeName}", typeName);
            ToolValidation.ThrowTypeNotFound(typeName);
        }

        logger.LogDebug("Found API reference for {TypeName} with {MemberCount} members",
            typeName, apiRef.Members?.Count ?? 0);

        var sb = new StringBuilder();
        sb.AppendLine($"# {apiRef.TypeName} API Reference (v{versionContext.Version})");
        sb.AppendLine();
        sb.AppendLine($"**Namespace:** `{apiRef.Namespace}`");
        
        if (!string.IsNullOrEmpty(apiRef.BaseType))
        {
            sb.AppendLine($"**Base Type:** `{apiRef.BaseType}`");
        }
        sb.AppendLine();

        if (!string.IsNullOrEmpty(apiRef.Summary))
        {
            sb.AppendLine("## Summary");
            sb.AppendLine();
            sb.AppendLine(apiRef.Summary);
            sb.AppendLine();
        }

        // Filter members
        var members = apiRef.Members ?? [];
        if (!effectiveMemberType.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var filterType = effectiveMemberType.ToLowerInvariant() switch
            {
                "properties" => "Property",
                "methods" => "Method",
                "events" => "Event",
                _ => effectiveMemberType
            };
            
            members = members.Where(m => 
                m.MemberType.Equals(filterType, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Group by member type
        var properties = members.Where(m => m.MemberType == "Property").ToList();
        var events = members.Where(m => m.MemberType == "Event").ToList();
        var methods = members.Where(m => m.MemberType == "Method").ToList();

        // Properties
        if (properties.Count > 0)
        {
            sb.AppendLine("## Properties");
            sb.AppendLine();
            sb.AppendLine("| Name | Type | Description |");
            sb.AppendLine("|------|------|-------------|");
            
            foreach (var prop in properties.OrderBy(p => p.Name))
            {
                var desc = ToolFormatting.Truncate(prop.Description, 60, collapseNewlines: true);
                sb.AppendLine($"| `{prop.Name}` | `{prop.ReturnType}` | {desc} |");
            }
            sb.AppendLine();
        }

        // Events
        if (events.Count > 0)
        {
            sb.AppendLine("## Events");
            sb.AppendLine();
            sb.AppendLine("| Name | Type | Description |");
            sb.AppendLine("|------|------|-------------|");
            
            foreach (var evt in events.OrderBy(e => e.Name))
            {
                var desc = ToolFormatting.Truncate(evt.Description, 60, collapseNewlines: true);
                sb.AppendLine($"| `{evt.Name}` | `{evt.ReturnType}` | {desc} |");
            }
            sb.AppendLine();
        }

        // Methods
        if (methods.Count > 0)
        {
            sb.AppendLine("## Methods");
            sb.AppendLine();
            
            foreach (var method in methods.OrderBy(m => m.Name))
            {
                var parameters = method.ParameterSignature ?? "";
                
                sb.AppendLine($"### `{method.ReturnType} {method.Name}({parameters})`");
                
                if (!string.IsNullOrEmpty(method.Description))
                {
                    sb.AppendLine();
                    sb.AppendLine(method.Description);
                }
                sb.AppendLine();
            }
        }

        // Summary statistics
        sb.AppendLine("## Summary Statistics");
        sb.AppendLine();
        sb.AppendLine($"- **Properties:** {properties.Count}");
        sb.AppendLine($"- **Events:** {events.Count}");
        sb.AppendLine($"- **Methods:** {methods.Count}");

        return sb.ToString();
    }

    /// <summary>
    /// Gets enum values for a MudBlazor enum type.
    /// </summary>
    [McpServerTool(Name = "get_enum_values")]
    [Description("Gets all values for a MudBlazor enum type (e.g., Color, Size, Variant). Values are parsed from the configured MudBlazor version's source. If a component seems missing, verify the --version matches your project's MudBlazor PackageReference in the .csproj file.")]
    public static async Task<string> GetEnumValuesAsync(
        IComponentIndexer indexer,
        ILogger<ApiReferenceTools> logger,
        VersionContext versionContext,
        [Description("The enum name (e.g., 'Color', 'Size', 'Variant', 'Align')")]
        string enumName,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(enumName, nameof(enumName));

        logger.LogDebug("Getting enum values for: {EnumName}", enumName);

        var enumInfo = await indexer.GetEnumAsync(enumName, cancellationToken);

        if (enumInfo is null)
        {
            logger.LogWarning("Enum not found: {EnumName}", enumName);
            ToolValidation.ThrowTypeNotFound(enumName);
        }

        logger.LogDebug("Found {Count} values for enum {EnumName}", enumInfo.Values.Count, enumName);

        return RenderEnum(enumInfo, versionContext);
    }

    /// <summary>
    /// Renders an <see cref="EnumInfo"/> as Markdown, including a numeric column only when explicit values are declared.
    /// </summary>
    private static string RenderEnum(EnumInfo enumInfo, VersionContext versionContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {enumInfo.Name} Enum Values (v{versionContext.Version})");
        sb.AppendLine();

        sb.AppendLine($"**Namespace:** `{enumInfo.Namespace ?? "MudBlazor"}`");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(enumInfo.Summary))
        {
            sb.AppendLine(enumInfo.Summary);
            sb.AppendLine();
        }

        var hasExplicitValues = enumInfo.Values.Any(v => v.Value.HasValue);

        if (hasExplicitValues)
        {
            sb.AppendLine("| Value | Numeric | Description |");
            sb.AppendLine("|-------|---------|-------------|");
            foreach (var value in enumInfo.Values)
            {
                var numeric = value.Value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "";
                var description = ToolFormatting.Truncate(value.Description, 100, emptyPlaceholder: "", collapseNewlines: true);
                sb.AppendLine($"| `{value.Name}` | {numeric} | {description} |");
            }
        }
        else
        {
            sb.AppendLine("| Value | Description |");
            sb.AppendLine("|-------|-------------|");
            foreach (var value in enumInfo.Values)
            {
                var description = ToolFormatting.Truncate(value.Description, 100, emptyPlaceholder: "", collapseNewlines: true);
                sb.AppendLine($"| `{value.Name}` | {description} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Usage Example");
        sb.AppendLine();
        sb.AppendLine("```razor");
        var firstValue = enumInfo.Values.Count > 0 ? enumInfo.Values[0].Name : "Value";
        sb.AppendLine($"<MudComponent {enumInfo.Name}=\"{enumInfo.Name}.{firstValue}\" />");
        sb.AppendLine("```");

        return sb.ToString();
    }
}
