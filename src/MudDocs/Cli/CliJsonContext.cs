// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;
using MudDocs.Models;

namespace MudDocs.Cli;

/// <summary>
/// A cached-version status entry reported by the <c>index-status</c> command.
/// </summary>
/// <param name="Version">The MudBlazor version (without the leading <c>v</c>).</param>
/// <param name="State">The cache state: <c>ready</c>, <c>stale</c>, or <c>not-indexed</c>.</param>
/// <param name="ComponentCount">The number of components in the store, or 0 when absent.</param>
/// <param name="EnumCount">The number of enums in the store, or 0 when absent.</param>
/// <param name="BuiltAtUtc">When the store was built, or <see langword="null"/> when no manifest exists.</param>
/// <param name="HasIndexJson">Whether the legacy <c>index.json</c> is present for this version.</param>
internal sealed record IndexStatus(
    string Version,
    string State,
    int ComponentCount,
    int EnumCount,
    DateTimeOffset? BuiltAtUtc,
    bool HasIndexJson);

/// <summary>A catalog search result without member details or example source code.</summary>
internal sealed record ComponentSummary(
    string Name,
    string? Category,
    string Summary,
    string? BaseType,
    int ParameterCount,
    int EventCount,
    int MethodCount,
    int ExampleCount)
{
    internal static ComponentSummary From(ComponentInfo component) =>
        new(component.Name, component.Category, component.Summary, component.BaseType,
            component.Parameters.Count, component.Events.Count, component.Methods.Count, component.Examples.Count);
}

/// <summary>
/// Source-generated serialization metadata for CLI JSON output, using camelCase property names
/// and omitting default values to keep the payload compact.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(IndexStatus))]
[JsonSerializable(typeof(List<IndexStatus>))]
[JsonSerializable(typeof(ComponentInfo))]
[JsonSerializable(typeof(ComponentExample))]
[JsonSerializable(typeof(IReadOnlyList<ComponentExample>))]
[JsonSerializable(typeof(IReadOnlyList<ComponentParameter>))]
[JsonSerializable(typeof(IReadOnlyList<ComponentEvent>))]
[JsonSerializable(typeof(IReadOnlyList<ComponentMethod>))]
[JsonSerializable(typeof(ApiReference))]
[JsonSerializable(typeof(EnumInfo))]
[JsonSerializable(typeof(IReadOnlyList<ComponentSummary>))]
[JsonSerializable(typeof(IReadOnlyList<ComponentCategory>))]
internal sealed partial class CliJsonContext : JsonSerializerContext;
