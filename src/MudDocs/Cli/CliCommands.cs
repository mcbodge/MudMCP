// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO.Enumeration;
using System.Text.Json;
using MudDocs.Configuration;
using MudDocs.Hosting;
using MudDocs.Models;
using MudDocs.Services;
using MudDocs.Tools;

namespace MudDocs.Cli;

/// <summary>
/// Handlers for the CLI command surface (REQ-005). Additional handlers are added by later tasks.
/// </summary>
internal static class CliCommands
{
    private const string ReadyState = "ready";
    private const string StaleState = "stale";
    private const string NotIndexedState = "not-indexed";

    /// <summary>Implements <c>related</c> from the catalog using the shared relationship query.</summary>
    internal static Task<int> RelatedAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var type = args.GetValue("--type") ?? "all";
        if (!ComponentSearchTools.ValidRelationshipTypes.Contains(type, StringComparer.OrdinalIgnoreCase))
        {
            throw new CliException(CliExitCodes.Usage, "--type must be all, parent, child, sibling, or commonly_used_with");
        }

        var name = NormalizeName(args.Positionals[0]);
        var catalog = context.Store.ReadCatalog().ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        if (!catalog.TryGetValue(name, out var component) &&
            (name.StartsWith("Mud", StringComparison.OrdinalIgnoreCase) || !catalog.TryGetValue("Mud" + name, out component)))
        {
            throw ComponentNotFound(context, name, catalog.Keys);
        }

        var related = ComponentQueries.FindRelated(catalog, component.Name, ComponentSearchTools.ParseRelationshipType(type));
        var output = context.Format switch
        {
            CliApp.CliFormat.Markdown => ComponentSearchTools.RenderRelated(component, related, context.Version),
            CliApp.CliFormat.Json => JsonSerializer.Serialize<IReadOnlyList<ComponentSummary>>(
                related.Select(ComponentSummary.From).ToList(), CliJsonContext.Default.IReadOnlyListComponentSummary) + "\n",
            _ => CompactFormatter.Related(component, related, context.Version),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    /// <summary>Implements <c>list</c> with exact, case-insensitive category filtering.</summary>
    internal static Task<int> ListAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var catalog = context.Store.ReadCatalog();
        var category = args.GetValue("--category");
        var components = catalog;
        if (category is not null)
        {
            components = catalog.Where(c => string.Equals(c.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();
            if (components.Count == 0)
            {
                throw new CliException(CliExitCodes.NotFound, $"category '{category}' not found",
                    "categories: " + string.Join(", ", ComponentQueries.BuildCategories(catalog).Select(c => c.Name)));
            }

            category = components[0].Category;
        }

        var desc = args.HasFlag("--desc");
        var output = context.Format switch
        {
            CliApp.CliFormat.Markdown => ComponentListTools.RenderComponentList(components, category, desc, context.Version),
            CliApp.CliFormat.Json => JsonSerializer.Serialize<IReadOnlyList<ComponentSummary>>(
                components.Select(ComponentSummary.From).ToList(), CliJsonContext.Default.IReadOnlyListComponentSummary) + "\n",
            _ => CompactFormatter.List(components, category, desc, context.Version),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    /// <summary>Implements <c>categories</c>, retaining the shared Markdown renderer's output.</summary>
    internal static Task<int> CategoriesAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var catalog = context.Store.ReadCatalog();
        var categories = ComponentQueries.BuildCategories(catalog);
        if (context.Format == CliApp.CliFormat.Markdown)
        {
            return WriteMembersAsync(context,
                ComponentListTools.RenderCategories(categories, catalog, context.Version), cancellationToken);
        }

        // The shared query omits uncategorized components; compact and JSON include them.
        if (catalog.Any(c => string.IsNullOrEmpty(c.Category)))
        {
            categories = ComponentQueries.BuildCategories(catalog.Select(c =>
                string.IsNullOrEmpty(c.Category) ? c with { Category = "Uncategorized" } : c));
        }

        var output = context.Format == CliApp.CliFormat.Json
            ? JsonSerializer.Serialize(categories, CliJsonContext.Default.IReadOnlyListComponentCategory) + "\n"
            : CompactFormatter.Categories(categories, context.Version);
        return WriteMembersAsync(context, output, cancellationToken);
    }

    /// <summary>Implements <c>search</c> using the shared catalog query and renderers.</summary>
    internal static Task<int> SearchAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var searchIn = args.GetValue("--in") ?? "all";
        if (!ComponentSearchTools.ValidSearchInOptions.Contains(searchIn, StringComparer.OrdinalIgnoreCase))
        {
            throw new CliException(CliExitCodes.Usage, "--in must be name, description, parameters, examples, or all");
        }

        var limit = 10;
        if (args.GetValue("--limit") is { } value &&
            (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 50))
        {
            throw new CliException(CliExitCodes.Usage, "--limit must be an integer from 1 to 50");
        }

        var query = string.Join(" ", args.Positionals);
        var fields = ComponentSearchTools.ParseSearchFields(searchIn);
        var results = ComponentQueries.Search(context.Store.ReadCatalog(), query, fields, limit);
        var output = context.Format switch
        {
            CliApp.CliFormat.Markdown => ComponentSearchTools.RenderSearchResults(query, results, fields, context.Version),
            CliApp.CliFormat.Json => JsonSerializer.Serialize<IReadOnlyList<ComponentSummary>>(
                results.Select(ComponentSummary.From).ToList(), CliJsonContext.Default.IReadOnlyListComponentSummary) + "\n",
            _ => CompactFormatter.Search(query, context.Version, results),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    /// <summary>Implements <c>examples</c> with shared filtering and a default code limit of three.</summary>
    internal static Task<int> ExamplesAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var limit = 3;
        if (args.GetValue("--limit") is { } value &&
            (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 20))
        {
            throw new CliException(CliExitCodes.Usage, "--limit must be an integer from 1 to 20");
        }

        var component = LoadComponent(context, args.Positionals[0]);
        var filtered = ComponentExampleTools.FilterExamples(component.Examples, args.GetValue("--filter"));
        var namesOnly = args.HasFlag("--names-only");
        var output = context.Format switch
        {
            CliApp.CliFormat.Markdown => namesOnly
                ? ComponentExampleTools.RenderExampleList(component.Name, filtered, context.Version)
                : ComponentExampleTools.RenderExamples(component.Name, filtered, limit, context.Version),
            CliApp.CliFormat.Json => JsonSerializer.Serialize<IReadOnlyList<ComponentExample>>(
                namesOnly
                    ? filtered.Select(example => example with { RazorMarkup = null, CSharpCode = null }).ToList()
                    : filtered.Take(limit).ToList(),
                CliJsonContext.Default.IReadOnlyListComponentExample) + "\n",
            _ => CompactFormatter.Examples(component.Name, context.Version, filtered,
                component.Examples.Count, limit, namesOnly),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    /// <summary>Implements <c>example</c>, preferring an exact name before a substring match.</summary>
    internal static Task<int> ExampleAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var component = LoadComponent(context, args.Positionals[0]);
        var name = string.Join(" ", args.Positionals.Skip(1));
        var example = ComponentExampleTools.FindExample(component.Examples, name)
            ?? throw new CliException(CliExitCodes.NotFound, $"example '{name}' not found for {component.Name}",
                "available: " + string.Join(", ", component.Examples.Select(candidate => candidate.Name)));
        var output = context.Format switch
        {
            CliApp.CliFormat.Markdown => ComponentExampleTools.RenderExample(component.Name, example, context.Version),
            CliApp.CliFormat.Json => JsonSerializer.Serialize(example, CliJsonContext.Default.ComponentExample) + "\n",
            _ => CompactFormatter.Example(component.Name, context.Version, example),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    /// <summary>Implements <c>api</c>, including the exact enum output fallback.</summary>
    internal static Task<int> ApiAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var members = args.GetValue("--members") ?? "all";
        if (!ApiReferenceTools.ValidMemberTypes.Contains(members, StringComparer.OrdinalIgnoreCase))
        {
            throw new CliException(CliExitCodes.Usage, "--members must be all, properties, methods, or events");
        }

        var name = NormalizeName(args.Positionals[0]);
        var component = context.Store.TryReadComponent(name);
        if (component is null)
        {
            var enumInfo = context.Store.TryReadEnum(name);
            if (enumInfo is not null)
            {
                return WriteEnumAsync(context, args, enumInfo, cancellationToken);
            }

            throw MissingType(context, "type", name,
                context.Store.ReadCatalog().Select(c => c.Name).Concat(context.Store.ReadEnums().Select(e => e.Name)));
        }

        members = members.ToLowerInvariant();
        var output = context.Format switch
        {
            CliApp.CliFormat.Markdown => ApiReferenceTools.RenderApiReference(ToApiReference(component), members, context.Version),
            CliApp.CliFormat.Json => JsonSerializer.Serialize(
                FilterApiMembers(ToApiReference(component), members), CliJsonContext.Default.ApiReference) + "\n",
            _ => CompactFormatter.Api(component.Name, context.Version,
                members is "all" or "properties"
                    ? FilterMembers(component.Parameters, args, p => p.Name, p => p.IsInherited, p => p.DeclaringType)
                        .Select(PropertyLine).ToList() : [],
                members is "all" or "events"
                    ? FilterMembers(component.Events, args, e => e.Name, e => e.IsInherited, e => e.DeclaringType)
                        .Select(EventLine).ToList() : [],
                members is "all" or "methods"
                    ? FilterMembers(component.Methods, args, m => m.Name, m => m.IsInherited, m => m.DeclaringType)
                        .Select(MethodLine).ToList() : [],
                args.HasFlag("--desc")),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    private static ApiReference FilterApiMembers(ApiReference api, string members) =>
        api with
        {
            Members = api.Members!.Where(m => members == "all" || m.MemberType == (members switch
            {
                "properties" => "Property",
                "events" => "Event",
                _ => "Method",
            })).ToList(),
        };

    /// <summary>Implements <c>enum</c> with exact, case-insensitive name lookup.</summary>
    internal static Task<int> EnumAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var name = NormalizeName(args.Positionals[0]);
        var enumInfo = context.Store.TryReadEnum(name);
        if (enumInfo is null)
        {
            throw MissingType(context, "enum", name, context.Store.ReadEnums().Select(e => e.Name));
        }

        return WriteEnumAsync(context, args, enumInfo, cancellationToken);
    }

    private static Task<int> WriteEnumAsync(
        CliApp.CliContext context, CliArgs args, EnumInfo enumInfo, CancellationToken cancellationToken)
    {
        var output = context.Format switch
        {
            CliApp.CliFormat.Markdown => ApiReferenceTools.RenderEnum(enumInfo, context.Version),
            CliApp.CliFormat.Json => JsonSerializer.Serialize(enumInfo, CliJsonContext.Default.EnumInfo) + "\n",
            _ => CompactFormatter.Enum(enumInfo, context.Version, args.HasFlag("--desc")),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    private static string NormalizeName(string input) =>
        IndexStore.NormalizeName(input) ?? throw new CliException(CliExitCodes.Usage, $"invalid name '{input}'");

    private static CliException MissingType(CliApp.CliContext context, string kind, string name, IEnumerable<string> candidates)
    {
        var suggestions = Suggest(candidates, name);
        return new CliException(CliExitCodes.NotFound, $"{kind} '{name}' not found for MudBlazor v{context.Version}",
            suggestions.Count > 0 ? "did you mean: " + string.Join(", ", suggestions)
                : $"muddocs search {name} --version {context.Version}");
    }

    private static CompactFormatter.MemberLine PropertyLine(ComponentParameter parameter) =>
        new($"{parameter.Name}: {parameter.Type}" + (parameter.DefaultValue is null ? "" : $" = {parameter.DefaultValue}") +
            (parameter.IsRequired ? " [required]" : "") + (parameter.IsCascading ? " [cascading]" : ""),
            parameter.Description, parameter.IsInherited, parameter.DeclaringType);

    private static CompactFormatter.MemberLine EventLine(ComponentEvent evt) =>
        new($"{evt.Name}: {EventType(evt)}", evt.Description, evt.IsInherited, evt.DeclaringType);

    private static CompactFormatter.MemberLine MethodLine(ComponentMethod method) =>
        new($"{method.ReturnType} {method.Name}({ParameterSignature(method)})",
            method.Description, method.IsInherited, method.DeclaringType);

    /// <summary>Implements <c>props</c> with category, required, and shared member filters.</summary>
    internal static Task<int> PropsAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var component = LoadComponent(context, args.Positionals[0]);
        IEnumerable<ComponentParameter> parameters = component.Parameters;
        if (args.GetValue("--category") is { } category)
        {
            parameters = parameters.Where(parameter => string.Equals(parameter.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (args.HasFlag("--required-only"))
        {
            parameters = parameters.Where(parameter => parameter.IsRequired);
        }

        var filtered = FilterMembers(parameters, args, p => p.Name, p => p.IsInherited, p => p.DeclaringType);
        var output = context.Format switch
        {
            CliApp.CliFormat.Json => JsonSerializer.Serialize(filtered, CliJsonContext.Default.IReadOnlyListComponentParameter) + "\n",
            CliApp.CliFormat.Markdown => ComponentDetailTools.RenderParameters(component.Name, filtered, context.Version),
            _ => CompactFormatter.Members(component.Name, context.Version, "props", filtered.Count, component.Parameters.Count,
                filtered.Select(PropertyLine), args.HasFlag("--desc")),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    /// <summary>Implements <c>events</c> with shared member filtering and MCP Markdown.</summary>
    internal static Task<int> EventsAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var component = LoadComponent(context, args.Positionals[0]);
        var filtered = FilterMembers(component.Events, args, e => e.Name, e => e.IsInherited, e => e.DeclaringType);
        var output = context.Format switch
        {
            CliApp.CliFormat.Json => JsonSerializer.Serialize(filtered, CliJsonContext.Default.IReadOnlyListComponentEvent) + "\n",
            CliApp.CliFormat.Markdown => ApiReferenceTools.RenderApiReference(
                ToApiReference(component with { Events = filtered }), "events", context.Version),
            _ => CompactFormatter.Members(component.Name, context.Version, "events", filtered.Count, component.Events.Count,
                filtered.Select(EventLine), args.HasFlag("--desc")),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    /// <summary>Implements <c>methods</c> with shared member filtering and MCP Markdown.</summary>
    internal static Task<int> MethodsAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var component = LoadComponent(context, args.Positionals[0]);
        var filtered = FilterMembers(component.Methods, args, m => m.Name, m => m.IsInherited, m => m.DeclaringType);
        var output = context.Format switch
        {
            CliApp.CliFormat.Json => JsonSerializer.Serialize(filtered, CliJsonContext.Default.IReadOnlyListComponentMethod) + "\n",
            CliApp.CliFormat.Markdown => ApiReferenceTools.RenderApiReference(
                ToApiReference(component with { Methods = filtered }), "methods", context.Version),
            _ => CompactFormatter.Members(component.Name, context.Version, "methods", filtered.Count, component.Methods.Count,
                filtered.Select(MethodLine), args.HasFlag("--desc")),
        };
        return WriteMembersAsync(context, output, cancellationToken);
    }

    private static IReadOnlyList<T> FilterMembers<T>(
        IEnumerable<T> members, CliArgs args, Func<T, string> name,
        Func<T, bool> inherited, Func<T, string?> declaringType)
    {
        var limit = 1000;
        if (args.GetValue("--limit") is { } value &&
            (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out limit) || limit is < 1 or > 1000))
        {
            throw new CliException(CliExitCodes.Usage, "--limit must be an integer from 1 to 1000");
        }

        if (args.HasFlag("--no-inherited"))
        {
            members = members.Where(member => !inherited(member));
        }

        if (args.GetValue("--filter") is { } filter)
        {
            var patterns = filter.Split(',', StringSplitOptions.TrimEntries);
            members = members.Where(member => patterns.Any(pattern =>
                FileSystemName.MatchesSimpleExpression(pattern, name(member), ignoreCase: true)));
        }

        var ordered = members.OrderBy(inherited)
            .ThenBy(member => inherited(member) ? declaringType(member) ?? "base" : "", StringComparer.OrdinalIgnoreCase)
            .ThenBy(name, StringComparer.OrdinalIgnoreCase);
        return args.HasFlag("--limit") ? ordered.Take(limit).ToList() : ordered.ToList();
    }

    private static string EventType(ComponentEvent evt) =>
        evt.EventArgsType is null ? "EventCallback" : $"EventCallback<{evt.EventArgsType}>";

    private static string? ParameterSignature(ComponentMethod method) =>
        method.Parameters.Count == 0 ? null : string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"));

    private static ApiReference ToApiReference(ComponentInfo component) =>
        new(component.Name, component.Namespace, component.Summary, component.BaseType,
        [
            .. component.Parameters.Select(p => new ApiMember(p.Name, "Property", p.Type, p.Description)),
            .. component.Events.Select(e => new ApiMember(e.Name, "Event", EventType(e), e.Description)),
            .. component.Methods.Select(m => new ApiMember(m.Name, "Method", m.ReturnType, m.Description, ParameterSignature(m))),
        ]);

    private static async Task<int> WriteMembersAsync(CliApp.CliContext context, string output, CancellationToken cancellationToken)
    {
        await context.Out.WriteAsync(output.AsMemory(), cancellationToken).ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    /// <summary>
    /// Implements <c>component</c>: a compact overview, MCP-equivalent Markdown, or metadata JSON.
    /// </summary>
    internal static async Task<int> ComponentAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var component = LoadComponent(context, args.Positionals[0]);
        var noInherited = args.HasFlag("--no-inherited");
        string output;
        if (context.Format == CliApp.CliFormat.Markdown)
        {
            output = ComponentDetailTools.RenderDetail(
                component, includeInheritedMembers: !noInherited,
                includeExamples: !args.HasFlag("--no-examples"), context.Version);
        }
        else
        {
            if (noInherited)
            {
                component = component with
                {
                    Parameters = component.Parameters.Where(parameter => !parameter.IsInherited).ToList(),
                    Events = component.Events.Where(evt => !evt.IsInherited).ToList(),
                    Methods = component.Methods.Where(method => !method.IsInherited).ToList(),
                };
            }

            if (context.Format == CliApp.CliFormat.Json)
            {
                component = component with
                {
                    Examples = component.Examples
                        .Select(example => example with { RazorMarkup = null, CSharpCode = null })
                        .ToList(),
                };
                output = JsonSerializer.Serialize(component, CliJsonContext.Default.ComponentInfo) + "\n";
            }
            else
            {
                output = CompactFormatter.Component(component, context.Version);
            }
        }

        await context.Out.WriteAsync(output.AsMemory(), cancellationToken).ConfigureAwait(false);
        return CliExitCodes.Success;
    }

    private static ComponentInfo LoadComponent(CliApp.CliContext context, string input)
    {
        var name = IndexStore.NormalizeName(input);
        if (name is null)
        {
            throw new CliException(CliExitCodes.Usage, $"invalid name '{input}'");
        }

        var component = context.Store.TryReadComponent(name);
        if (component is not null)
        {
            return component;
        }

        throw ComponentNotFound(context, name, context.Store.ReadCatalog().Select(candidate => candidate.Name));
    }

    private static CliException ComponentNotFound(CliApp.CliContext context, string name, IEnumerable<string> names)
    {
        var enumInfo = context.Store.TryReadEnum(name);
        string hint;
        if (enumInfo is not null)
        {
            hint = $"muddocs enum {enumInfo.Name} --version {context.Version}";
        }
        else
        {
            var suggestions = Suggest(names, name);
            hint = suggestions.Count > 0
                ? "did you mean: " + string.Join(", ", suggestions)
                : $"muddocs search {name} --version {context.Version}";
        }

        return new CliException(
            CliExitCodes.NotFound, $"component '{name}' not found for MudBlazor v{context.Version}", hint);
    }

    private static IReadOnlyList<string> Suggest(IEnumerable<string> names, string query)
    {
        static string WithoutMud(string name) =>
            name.StartsWith("Mud", StringComparison.OrdinalIgnoreCase) ? name[3..] : name;

        var shortQuery = WithoutMud(query);
        return names
            .Where(name => WithoutMud(name).Contains(shortQuery, StringComparison.OrdinalIgnoreCase)
                || shortQuery.Contains(WithoutMud(name), StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => Math.Abs(WithoutMud(name).Length - shortQuery.Length))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToArray();
    }

    /// <summary>
    /// Implements <c>index-status</c> (REQ-019): reports the cached index state for one version
    /// (when <see cref="CliApp.CliContext.Version"/> is set) or for every <c>v*</c> folder under the
    /// data path. Compact and markdown output lead with a <c>data-path</c> line; JSON output is the
    /// bare status payload. A requested version that is not ready exits 3 with the <c>index</c> hint.
    /// </summary>
    internal static async Task<int> IndexStatusAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var dataPath = context.DataPath;
        var json = context.Format == CliApp.CliFormat.Json;

        if (!json)
        {
            await context.Out.WriteLineAsync($"data-path {dataPath}".AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(context.Version))
        {
            var status = DeriveStatus(context.Version, dataPath);

            if (json)
            {
                var payload = JsonSerializer.Serialize(status, CliJsonContext.Default.IndexStatus);
                await context.Out.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await context.Out.WriteLineAsync(CompactFormatter.IndexStatusLine(status).AsMemory(), cancellationToken).ConfigureAwait(false);
            }

            if (string.Equals(status.State, ReadyState, StringComparison.Ordinal))
            {
                return CliExitCodes.Success;
            }

            throw new CliException(
                CliExitCodes.NotIndexed,
                $"MudBlazor v{context.Version} is not indexed in {dataPath}",
                $"muddocs index --version {context.Version}");
        }

        var statuses = EnumerateStatuses(dataPath);

        if (json)
        {
            var payload = JsonSerializer.Serialize(statuses, CliJsonContext.Default.ListIndexStatus);
            await context.Out.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            foreach (var status in statuses)
            {
                await context.Out.WriteLineAsync(CompactFormatter.IndexStatusLine(status).AsMemory(), cancellationToken).ConfigureAwait(false);
            }
        }

        return CliExitCodes.Success;
    }

    /// <summary>
    /// Implements <c>index</c> (REQ-018, REQ-026): rebuilds the on-disk store for the requested
    /// version. The stale store is removed first, then the index is (re)built — loading a valid
    /// cached <c>index.json</c> when present so no repository clone is required — and the resulting
    /// <see cref="IndexStatus"/> is printed. A build failure exits 1; a build that leaves no current
    /// store also exits 1.
    /// </summary>
    internal static async Task<int> IndexAsync(CliApp.CliContext context, CliArgs args, CancellationToken cancellationToken)
    {
        var versionContext = new VersionContext(context.Version, context.DataPath);
        if (Directory.Exists(versionContext.StorePath))
        {
            Directory.Delete(versionContext.StorePath, recursive: true);
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MudBlazor:Repository:DataPath"] = context.DataPath,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            McpServerHost.ConfigureLogging(logging);
            logging.SetMinimumLevel(LogLevel.Information);
        });
        McpServerHost.RegisterCoreServices(services, configuration, context.Version);

        await using var provider = services.BuildServiceProvider();

        try
        {
            var indexer = provider.GetRequiredService<IComponentIndexer>();
            await indexer.BuildIndexAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or LibGit2Sharp.LibGit2SharpException or IOException or UnauthorizedAccessException)
        {
            throw new CliException(CliExitCodes.Error, "index build failed: " + ex.Message);
        }

        var store = new IndexStore(versionContext.StorePath);
        var status = DeriveStatus(context.Version, context.DataPath);

        if (context.Format == CliApp.CliFormat.Json)
        {
            var payload = JsonSerializer.Serialize(status, CliJsonContext.Default.IndexStatus);
            await context.Out.WriteLineAsync(payload.AsMemory(), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await context.Out.WriteLineAsync(CompactFormatter.IndexStatusLine(status).AsMemory(), cancellationToken).ConfigureAwait(false);
        }

        return store.IsCurrent(context.Version)
            ? CliExitCodes.Success
            : throw new CliException(CliExitCodes.Error, "index built but the CLI store could not be written to " + versionContext.StorePath);
    }

    private static List<IndexStatus> EnumerateStatuses(string dataPath)
    {
        var statuses = new List<IndexStatus>();
        if (!Directory.Exists(dataPath))
        {
            return statuses;
        }

        var versionFolders = Directory.EnumerateDirectories(dataPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith('v'))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var folder in versionFolders)
        {
            statuses.Add(DeriveStatus(folder![1..], dataPath));
        }

        return statuses;
    }

    private static IndexStatus DeriveStatus(string version, string dataPath)
    {
        var versionContext = new VersionContext(version, dataPath);
        var store = new IndexStore(versionContext.StorePath);
        var manifest = store.TryReadManifest();
        var hasIndexJson = File.Exists(versionContext.IndexPath);

        var state = store.IsCurrent(version)
            ? ReadyState
            : manifest is not null ? StaleState : NotIndexedState;

        return new IndexStatus(
            Version: version,
            State: state,
            ComponentCount: manifest?.ComponentCount ?? 0,
            EnumCount: manifest?.EnumCount ?? 0,
            BuiltAtUtc: manifest?.BuiltAtUtc,
            HasIndexJson: hasIndexJson);
    }

    /// <summary>
    /// Returns the versions under <paramref name="dataPath"/> that have a current (ready) CLI store.
    /// The <c>v*</c> folders are enumerated in ordinal order and the leading <c>v</c> is stripped;
    /// only versions whose store <see cref="IndexStore.IsCurrent"/> check passes are returned. The
    /// list is empty when the data path does not exist.
    /// </summary>
    internal static IReadOnlyList<string> ReadyVersions(string dataPath)
    {
        var ready = new List<string>();
        if (!Directory.Exists(dataPath))
        {
            return ready;
        }

        var versionFolders = Directory.EnumerateDirectories(dataPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name) && name.StartsWith('v'))
            .OrderBy(name => name, StringComparer.Ordinal);

        foreach (var folder in versionFolders)
        {
            var version = folder![1..];
            if (new IndexStore(new VersionContext(version, dataPath).StorePath).IsCurrent(version))
            {
                ready.Add(version);
            }
        }

        return ready;
    }
}
