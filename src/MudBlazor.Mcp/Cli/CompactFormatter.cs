// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services;
using MudBlazor.Mcp.Tools;

namespace MudBlazor.Mcp.Cli;

/// <summary>
/// Renders CLI command output in the compact text format (FMT-001 to FMT-008).
/// Additional members are added by later tasks.
/// </summary>
internal static class CompactFormatter
{
    /// <summary>Renders non-empty relationship groups in shared query order (FMT-006).</summary>
    internal static string Related(ComponentInfo component, IReadOnlyList<ComponentInfo> related, string version)
    {
        var builder = new StringBuilder();
        builder.Append(component.Name).Append(" v").Append(version).Append(" related ").Append(related.Count).Append('\n');
        var (parent, children, siblings, others) = ComponentQueries.GroupRelated(component, related);
        if (parent is not null)
        {
            builder.Append("parent: ").Append(parent.Name).Append('\n');
        }

        AppendGroup("children", children);
        AppendGroup("siblings", siblings);
        AppendGroup("other", others);
        return builder.ToString();

        void AppendGroup(string label, IReadOnlyList<ComponentInfo> components)
        {
            if (components.Count > 0)
            {
                builder.Append(label).Append(": ").AppendJoin(", ", components.Select(c => c.Name)).Append('\n');
            }
        }
    }

    /// <summary>Renders grouped component names or one summary per component (FMT-006).</summary>
    internal static string List(IReadOnlyList<ComponentInfo> components, string? category, bool desc, string version)
    {
        var builder = new StringBuilder();
        builder.Append(category ?? "MudBlazor").Append(" v").Append(version).Append(" list ").Append(components.Count).Append('\n');
        if (desc)
        {
            foreach (var component in components.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                AppendSummary(builder, component);
            }
        }
        else
        {
            foreach (var group in components
                .GroupBy(c => string.IsNullOrEmpty(c.Category) ? "Uncategorized" : c.Category, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
            {
                builder.Append(group.Key).Append(": ")
                    .AppendJoin(", ", group.Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
                    .Append('\n');
            }
        }

        return builder.ToString();
    }

    /// <summary>Renders category names and component counts (FMT-006).</summary>
    internal static string Categories(IReadOnlyList<ComponentCategory> categories, string version)
    {
        var builder = new StringBuilder();
        builder.Append("MudBlazor v").Append(version).Append(" categories ").Append(categories.Count).Append('\n');
        foreach (var category in categories)
        {
            builder.Append(category.Name).Append(' ').Append(category.ComponentNames.Count).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>Renders search results in score order with short summaries (FMT-001, FMT-006).</summary>
    internal static string Search(string query, string version, IReadOnlyList<ComponentInfo> results)
    {
        var builder = new StringBuilder();
        builder.Append('"').Append(query).Append("\" v").Append(version).Append(" search ").Append(results.Count).Append('\n');
        foreach (var component in results)
        {
            AppendSummary(builder, component);
        }

        return builder.ToString();
    }

    private static void AppendSummary(StringBuilder builder, ComponentInfo component) =>
        builder.Append(component.Name).Append(" [").Append(component.Category ?? "-").Append("] ")
            .Append(ToolFormatting.Truncate(component.Summary, 80, emptyPlaceholder: "", collapseNewlines: true))
            .Append('\n');

    /// <summary>Renders example names or limited code blocks in stored order (FMT-007).</summary>
    internal static string Examples(
        string componentName, string version, IReadOnlyList<ComponentExample> examples,
        int total, int limit, bool namesOnly)
    {
        var builder = new StringBuilder();
        builder.Append(componentName).Append(" v").Append(version).Append(" examples ");
        if (namesOnly)
        {
            builder.Append(examples.Count).Append('\n');
            foreach (var example in examples)
            {
                builder.Append(example.Name);
                if (example.Features.Count > 0)
                {
                    builder.Append(" [").AppendJoin(", ", example.Features.Take(3)).Append(']');
                }

                builder.Append('\n');
            }
        }
        else
        {
            builder.Append(Math.Min(limit, examples.Count)).Append('/').Append(total).Append('\n');
            foreach (var example in examples.Take(limit))
            {
                AppendExample(builder, example);
            }
        }

        return builder.ToString();
    }

    /// <summary>Renders a selected example with its resolved name in the header.</summary>
    internal static string Example(string componentName, string version, ComponentExample example)
    {
        var builder = new StringBuilder();
        builder.Append(componentName).Append(" v").Append(version).Append(" example ").Append(example.Name).Append('\n');
        AppendExample(builder, example);
        return builder.ToString();
    }

    private static void AppendExample(StringBuilder builder, ComponentExample example)
    {
        builder.Append("=== ").Append(example.Name).Append('\n');
        builder.Append(Dedent(example.RazorMarkup));
        var code = Dedent(example.CSharpCode);
        if (code.Length > 0)
        {
            builder.Append("--- code\n").Append(code);
        }
    }

    /// <summary>
    /// Removes common indentation and trailing whitespace, trims outer blank lines, and collapses
    /// blank runs to one. Non-empty output ends with one LF.
    /// </summary>
    internal static string Dedent(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }

        var lines = code.ReplaceLineEndings("\n").Split('\n').Select(line => line.TrimEnd()).ToArray();
        var indent = lines.Where(line => line.Length > 0)
            .Min(line => line.TakeWhile(char.IsWhiteSpace).Count());
        var first = Array.FindIndex(lines, line => line.Length > 0);
        var last = Array.FindLastIndex(lines, line => line.Length > 0);
        var builder = new StringBuilder();
        var previousBlank = false;
        for (var index = first; index <= last; index++)
        {
            var blank = lines[index].Length == 0;
            if (!blank)
            {
                builder.Append(lines[index].AsSpan(indent)).Append('\n');
            }
            else if (!previousBlank)
            {
                builder.Append('\n');
            }

            previousBlank = blank;
        }

        return builder.ToString();
    }

    /// <summary>A rendered member signature with its description and inheritance metadata.</summary>
    internal sealed record MemberLine(string Signature, string? Description, bool IsInherited, string? DeclaringType);

    /// <summary>Renders API sections using the shared member line format (FMT-008).</summary>
    internal static string Api(
        string name, string version, IReadOnlyList<MemberLine> properties,
        IReadOnlyList<MemberLine> events, IReadOnlyList<MemberLine> methods, bool includeDescriptions)
    {
        var builder = new StringBuilder();
        builder.Append(name).Append(" v").Append(version).Append(" api\n");
        AppendSection("props", properties);
        AppendSection("events", events);
        AppendSection("methods", methods);
        return builder.ToString();

        void AppendSection(string section, IReadOnlyList<MemberLine> lines)
        {
            if (lines.Count == 0)
            {
                return;
            }

            builder.Append(section).Append(' ').Append(lines.Count).Append('\n');
            AppendMembers(builder, lines, includeDescriptions);
        }
    }

    /// <summary>Renders enum values in source order with explicit numeric values (FMT-008).</summary>
    internal static string Enum(EnumInfo enumInfo, string version, bool includeDescriptions)
    {
        static string Signature(EnumValueInfo value) => value.Value is { } number
            ? $"{value.Name}={number.ToString(CultureInfo.InvariantCulture)}" : value.Name;

        var builder = new StringBuilder();
        builder.Append(enumInfo.Name).Append(" v").Append(version).Append(" enum ").Append(enumInfo.Values.Count);
        if (includeDescriptions)
        {
            builder.Append('\n');
            AppendMembers(builder, enumInfo.Values.Select(value =>
                new MemberLine(Signature(value), value.Description, false, null)), includeDescriptions: true);
        }
        else
        {
            if (enumInfo.Values.Count > 0)
            {
                builder.Append(": ").AppendJoin(", ", enumInfo.Values.Select(Signature));
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>Renders an already ordered, filtered member list (FMT-001 to FMT-004).</summary>
    internal static string Members(
        string componentName, string version, string command, int shown, int total,
        IEnumerable<MemberLine> members, bool includeDescriptions)
    {
        var builder = new StringBuilder();
        builder.Append(componentName).Append(" v").Append(version).Append(' ').Append(command)
            .Append(' ').Append(shown).Append('/').Append(total).Append('\n');
        AppendMembers(builder, members, includeDescriptions);
        return builder.ToString();
    }

    private static void AppendMembers(StringBuilder builder, IEnumerable<MemberLine> members, bool includeDescriptions)
    {
        string? previousGroup = null;
        foreach (var member in members)
        {
            if (member.IsInherited)
            {
                var group = member.DeclaringType ?? "base";
                if (!string.Equals(group, previousGroup, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Append("-- inherited from ").Append(group).Append('\n');
                    previousGroup = group;
                }
            }

            builder.Append(member.Signature);
            if (includeDescriptions)
            {
                var description = ToolFormatting.Truncate(member.Description, 80, emptyPlaceholder: "", collapseNewlines: true);
                if (!string.IsNullOrEmpty(description))
                {
                    builder.Append("  // ").Append(description);
                }
            }

            builder.Append('\n');
        }
    }

    /// <summary>Renders a component overview without member details or example code (FMT-005).</summary>
    internal static string Component(ComponentInfo component, string version)
    {
        var builder = new StringBuilder();
        builder.Append(component.Name).Append(" v").Append(version).Append(" component\n");
        builder.Append(component.Namespace).Append('.').Append(component.Name);
        if (!string.IsNullOrEmpty(component.BaseType))
        {
            builder.Append(" : ").Append(component.BaseType);
        }

        if (!string.IsNullOrEmpty(component.Category))
        {
            builder.Append(" [").Append(component.Category).Append(']');
        }

        builder.Append('\n');
        var summary = ToolFormatting.Truncate(component.Summary, 160, emptyPlaceholder: "", collapseNewlines: true);
        if (!string.IsNullOrEmpty(summary))
        {
            builder.Append(summary).Append('\n');
        }

        if (component.Parameters.Count > 0)
        {
            var ownNames = component.Parameters.Where(parameter => !parameter.IsInherited)
                .Select(parameter => parameter.Name).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
            builder.Append("props ").Append(component.Parameters.Count)
                .Append(" (own ").Append(ownNames.Length).Append("): ")
                .AppendJoin(", ", ownNames).Append('\n');
        }

        if (component.Events.Count > 0)
        {
            builder.Append("events ").Append(component.Events.Count).Append(": ")
                .AppendJoin(", ", component.Events.OrderBy(evt => evt.IsInherited)
                    .ThenBy(evt => evt.Name, StringComparer.OrdinalIgnoreCase).Select(evt => evt.Name)).Append('\n');
        }

        if (component.Methods.Count > 0)
        {
            builder.Append("methods ").Append(component.Methods.Count).Append(": ")
                .AppendJoin(", ", component.Methods.OrderBy(method => method.IsInherited)
                    .ThenBy(method => method.Name, StringComparer.OrdinalIgnoreCase).Select(method => method.Name)).Append('\n');
        }

        if (component.Examples.Count > 0)
        {
            builder.Append("examples ").Append(component.Examples.Count).Append(": ")
                .AppendJoin(", ", component.Examples.Select(example => example.Name)).Append('\n');
        }

        if (component.RelatedComponents.Count > 0)
        {
            builder.Append("related: ").AppendJoin(", ", component.RelatedComponents).Append('\n');
        }

        if (!string.IsNullOrEmpty(component.DocumentationUrl))
        {
            builder.Append("docs: ").Append(component.DocumentationUrl).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Renders a single <see cref="IndexStatus"/> as a compact line: version and state, then the
    /// component and enum counts plus build time when a manifest exists, then an <c>index.json</c>
    /// marker when that file is present.
    /// </summary>
    internal static string IndexStatusLine(IndexStatus status)
    {
        var builder = new StringBuilder();
        builder.Append(status.Version).Append(' ').Append(status.State);

        if (status.BuiltAtUtc is { } builtAt)
        {
            builder
                .Append(" components=").Append(status.ComponentCount.ToString(CultureInfo.InvariantCulture))
                .Append(" enums=").Append(status.EnumCount.ToString(CultureInfo.InvariantCulture))
                .Append(" built=").Append(builtAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        }

        if (status.HasIndexJson)
        {
            builder.Append(" index.json=present");
        }

        return builder.ToString();
    }
}
