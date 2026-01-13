// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
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
    [Description("Gets the full API reference for a MudBlazor component or type, including all properties, methods, and events.")]
    public static async Task<string> GetApiReferenceAsync(
        IComponentIndexer indexer,
        ILogger<ApiReferenceTools> logger,
        [Description("The type name (e.g., 'MudButton', 'Color', 'Size')")]
        string typeName,
        [Description("Filter to specific member type: 'all', 'properties', 'methods', 'events' (default: 'all')")]
        string memberType = "all",
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(typeName, nameof(typeName));
        ToolValidation.RequireValidOption(memberType, ValidMemberTypes, nameof(memberType));

        logger.LogDebug("Getting API reference for type: {TypeName}, memberType: {MemberType}",
            typeName, memberType);

        var apiRef = await indexer.GetApiReferenceAsync(typeName, cancellationToken);
        
        if (apiRef is null)
        {
            logger.LogWarning("Type not found: {TypeName}", typeName);
            ToolValidation.ThrowTypeNotFound(typeName);
        }

        logger.LogDebug("Found API reference for {TypeName} with {MemberCount} members",
            typeName, apiRef.Members?.Count ?? 0);

        var sb = new StringBuilder();
        sb.AppendLine($"# {apiRef.TypeName} API Reference");
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
        if (!memberType.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var filterType = memberType.ToLowerInvariant() switch
            {
                "properties" => "Property",
                "methods" => "Method",
                "events" => "Event",
                _ => memberType
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
                var desc = TruncateText(prop.Description, 60);
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
                var desc = TruncateText(evt.Description, 60);
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
    [Description("Gets all values for a MudBlazor enum type (e.g., Color, Size, Variant).")]
    public static async Task<string> GetEnumValuesAsync(
        ILogger<ApiReferenceTools> logger,
        [Description("The enum name (e.g., 'Color', 'Size', 'Variant', 'Align')")]
        string enumName,
        CancellationToken cancellationToken = default)
    {
        ToolValidation.RequireNonEmpty(enumName, nameof(enumName));

        logger.LogDebug("Getting enum values for: {EnumName}", enumName);

        // Common MudBlazor enums with their values
        var enumValues = GetKnownEnumValues(enumName);
        
        if (enumValues is null)
        {
            logger.LogWarning("Enum not found: {EnumName}", enumName);
            ToolValidation.ThrowTypeNotFound(enumName);
        }

        logger.LogDebug("Found {Count} values for enum {EnumName}", enumValues.Count, enumName);

        var sb = new StringBuilder();
        sb.AppendLine($"# {enumName} Enum Values");
        sb.AppendLine();
        sb.AppendLine("| Value | Description |");
        sb.AppendLine("|-------|-------------|");
        
        foreach (var (value, description) in enumValues)
        {
            sb.AppendLine($"| `{value}` | {description} |");
        }
        sb.AppendLine();
        
        sb.AppendLine("## Usage Example");
        sb.AppendLine();
        sb.AppendLine($"```razor");
        sb.AppendLine($"<MudComponent {enumName}=\"{enumName}.{enumValues[0].Value}\" />");
        sb.AppendLine($"```");

        return sb.ToString();
    }

    private static List<(string Value, string Description)>? GetKnownEnumValues(string enumName)
    {
        return enumName.ToLowerInvariant() switch
        {
            "color" => [
                ("Default", "Default theme color"),
                ("Primary", "Primary theme color (usually blue)"),
                ("Secondary", "Secondary theme color"),
                ("Tertiary", "Tertiary theme color"),
                ("Info", "Informational blue color"),
                ("Success", "Success green color"),
                ("Warning", "Warning yellow/orange color"),
                ("Error", "Error red color"),
                ("Dark", "Dark color"),
                ("Transparent", "Transparent (no color)"),
                ("Inherit", "Inherit color from parent"),
                ("Surface", "Surface background color")
            ],
            "size" => [
                ("Small", "Small size"),
                ("Medium", "Medium size (default)"),
                ("Large", "Large size")
            ],
            "variant" => [
                ("Text", "Text-only variant with no background"),
                ("Filled", "Filled variant with solid background"),
                ("Outlined", "Outlined variant with border only")
            ],
            "align" => [
                ("Start", "Align to start (left in LTR)"),
                ("Center", "Align to center"),
                ("End", "Align to end (right in LTR)"),
                ("Justify", "Justify content")
            ],
            "position" => [
                ("Top", "Position at top"),
                ("Right", "Position at right"),
                ("Bottom", "Position at bottom"),
                ("Left", "Position at left")
            ],
            "placement" => [
                ("Top", "Place at top"),
                ("Bottom", "Place at bottom"),
                ("Left", "Place at left"),
                ("Right", "Place at right"),
                ("Start", "Place at start"),
                ("End", "Place at end")
            ],
            "typo" => [
                ("h1", "Heading 1 (largest)"),
                ("h2", "Heading 2"),
                ("h3", "Heading 3"),
                ("h4", "Heading 4"),
                ("h5", "Heading 5"),
                ("h6", "Heading 6 (smallest heading)"),
                ("subtitle1", "Subtitle 1"),
                ("subtitle2", "Subtitle 2"),
                ("body1", "Body 1 text"),
                ("body2", "Body 2 text"),
                ("button", "Button text style"),
                ("caption", "Caption text style"),
                ("overline", "Overline text style")
            ],
            "edge" => [
                ("False", "No edge positioning"),
                ("Start", "Edge start position"),
                ("End", "Edge end position")
            ],
            "origin" => [
                ("TopLeft", "Origin at top left"),
                ("TopCenter", "Origin at top center"),
                ("TopRight", "Origin at top right"),
                ("CenterLeft", "Origin at center left"),
                ("CenterCenter", "Origin at center"),
                ("CenterRight", "Origin at center right"),
                ("BottomLeft", "Origin at bottom left"),
                ("BottomCenter", "Origin at bottom center"),
                ("BottomRight", "Origin at bottom right")
            ],
            "adornment" => [
                ("None", "No adornment"),
                ("Start", "Adornment at start"),
                ("End", "Adornment at end")
            ],
            "inputtype" => [
                ("Text", "Standard text input"),
                ("Password", "Password input (masked)"),
                ("Email", "Email input with validation"),
                ("Number", "Numeric input"),
                ("Telephone", "Telephone number input"),
                ("Search", "Search input"),
                ("Url", "URL input"),
                ("Date", "Date input"),
                ("Time", "Time input"),
                ("DateTimeLocal", "Local date/time input"),
                ("Month", "Month input"),
                ("Week", "Week input"),
                ("Color", "Color picker input"),
                ("Hidden", "Hidden input")
            ],
            "alignitems" => [
                ("Baseline", "Align items to their baseline"),
                ("Center", "Center items along the cross axis"),
                ("Start", "Align items to the start of the cross axis"),
                ("End", "Align items to the end of the cross axis"),
                ("Stretch", "Stretch items to fill the container (default)")
            ],
            "justify" => [
                ("FlexStart", "Pack items toward the start"),
                ("Center", "Pack items around the center"),
                ("FlexEnd", "Pack items toward the end"),
                ("SpaceBetween", "Distribute items evenly, first at start, last at end"),
                ("SpaceAround", "Distribute items evenly with equal space around them"),
                ("SpaceEvenly", "Distribute items evenly with equal space between them")
            ],
            _ => null
        };
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
