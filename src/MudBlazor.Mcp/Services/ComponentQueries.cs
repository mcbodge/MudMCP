// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudBlazor.Mcp.Models;

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Pure query algorithms over an in-memory component set, shared by the MCP server and the CLI.
/// Ordering is deterministic so identical inputs always yield identical results.
/// </summary>
internal static class ComponentQueries
{
    /// <summary>
    /// Reconstructs categories from the indexed components (categories are menu-derived and stored
    /// on each <see cref="ComponentInfo"/>), grouped by category name and sorted ordinal-ignore-case.
    /// </summary>
    internal static IReadOnlyList<ComponentCategory> BuildCategories(IEnumerable<ComponentInfo> components)
    {
        var categories = components
            .Where(c => !string.IsNullOrEmpty(c.Category))
            .GroupBy(c => c.Category!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ComponentCategory(
                Name: g.Key,
                Title: g.Key,
                Description: null,
                ComponentNames: g.Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return categories;
    }

    /// <summary>
    /// Scores each component against the query and returns the top matches, ordered by score
    /// descending and then by name (ordinal-ignore-case) so ties are deterministic.
    /// </summary>
    internal static IReadOnlyList<ComponentInfo> Search(
        IEnumerable<ComponentInfo> components,
        string query,
        SearchFields searchFields,
        int maxResults)
    {
        var queryLower = query.ToLowerInvariant();
        var results = new List<(ComponentInfo Component, int Score)>();

        foreach (var component in components)
        {
            var score = CalculateSearchScore(component, queryLower, searchFields);
            if (score > 0)
            {
                results.Add((component, score));
            }
        }

        var sorted = results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Component.Name, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(r => r.Component)
            .ToList();

        return sorted;
    }

    private static int CalculateSearchScore(ComponentInfo component, string query, SearchFields fields)
    {
        var score = 0;

        if (fields.HasFlag(SearchFields.Name))
        {
            if (component.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                score += component.Name.Equals(query, StringComparison.OrdinalIgnoreCase) ? 100 : 50;
            }
        }

        if (fields.HasFlag(SearchFields.Description))
        {
            if (component.Summary?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                score += 30;
            if (component.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                score += 20;
        }

        if (fields.HasFlag(SearchFields.Parameters))
        {
            foreach (var param in component.Parameters)
            {
                if (param.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    score += 10;
                if (param.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                    score += 5;
            }
        }

        if (fields.HasFlag(SearchFields.Examples))
        {
            foreach (var example in component.Examples)
            {
                if (example.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    score += 5;
            }
        }

        return score;
    }

    /// <summary>
    /// Resolves the components related to <paramref name="componentName"/> (explicit relations,
    /// same-category siblings, parent base type, and inheriting children). Siblings are inserted
    /// in name order (ordinal-ignore-case) so the capped result is deterministic.
    /// </summary>
    internal static IReadOnlyList<ComponentInfo> FindRelated(
        IReadOnlyDictionary<string, ComponentInfo> components,
        string componentName,
        RelationshipType relationshipType)
    {
        if (!components.TryGetValue(componentName, out var component))
        {
            return [];
        }

        var related = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add explicitly related components
        foreach (var relatedName in component.RelatedComponents)
        {
            related.Add(relatedName);
        }

        // Add components in same category (siblings)
        if (relationshipType is RelationshipType.All or RelationshipType.Sibling)
        {
            if (component.Category is not null)
            {
                var siblings = components.Values
                    .Where(c => c.Category?.Equals(component.Category, StringComparison.OrdinalIgnoreCase) == true
                                && !c.Name.Equals(componentName, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var sibling in siblings)
                {
                    related.Add(sibling.Name);
                }
            }
        }

        // Add parent (base type)
        if (relationshipType is RelationshipType.All or RelationshipType.Parent)
        {
            if (component.BaseType is not null && components.ContainsKey(component.BaseType))
            {
                related.Add(component.BaseType);
            }
        }

        // Add children (components that inherit from this one)
        if (relationshipType is RelationshipType.All or RelationshipType.Child)
        {
            foreach (var (name, comp) in components)
            {
                if (comp.BaseType?.Equals(componentName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    related.Add(name);
                }
            }
        }

        var relatedComponents = related
            .Where(r => components.ContainsKey(r))
            .Select(r => components[r])
            .Take(10)
            .ToList();

        return relatedComponents;
    }

    /// <summary>
    /// Partitions <paramref name="related"/> relative to <paramref name="component"/> into the parent
    /// (base type), children (inheritors), same-category siblings, and everything else. The buckets are
    /// disjoint and preserve input order within each group.
    /// </summary>
    internal static (ComponentInfo? Parent, IReadOnlyList<ComponentInfo> Children, IReadOnlyList<ComponentInfo> Siblings, IReadOnlyList<ComponentInfo> Others) GroupRelated(
        ComponentInfo component,
        IReadOnlyList<ComponentInfo> related)
    {
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

        return (parent, children, siblings, others);
    }
}
