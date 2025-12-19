// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudBlazor.Mcp.Models;

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Service for indexing and querying MudBlazor component documentation.
/// </summary>
public interface IComponentIndexer
{
    /// <summary>
    /// Gets whether the index has been built.
    /// </summary>
    bool IsIndexed { get; }

    /// <summary>
    /// Gets the timestamp of the last index build.
    /// </summary>
    DateTimeOffset? LastIndexed { get; }

    /// <summary>
    /// Builds or rebuilds the component index from the repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task BuildIndexAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all components in the index.
    /// </summary>
    Task<IReadOnlyList<ComponentInfo>> GetAllComponentsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific component by name.
    /// </summary>
    /// <param name="componentName">The component name (e.g., "MudButton").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ComponentInfo?> GetComponentAsync(string componentName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all component categories.
    /// </summary>
    Task<IReadOnlyList<ComponentCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets components in a specific category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ComponentInfo>> GetComponentsByCategoryAsync(string category, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches components by query.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="searchFields">Fields to search in.</param>
    /// <param name="maxResults">Maximum number of results.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ComponentInfo>> SearchComponentsAsync(
        string query,
        SearchFields searchFields = SearchFields.All,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets code examples for a component.
    /// </summary>
    /// <param name="componentName">The component name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ComponentExample>> GetExamplesAsync(string componentName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets API reference for a type.
    /// </summary>
    /// <param name="typeName">The type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<ApiReference?> GetApiReferenceAsync(string typeName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets components related to a specific component.
    /// </summary>
    /// <param name="componentName">The component name.</param>
    /// <param name="relationshipType">Type of relationship to find.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ComponentInfo>> GetRelatedComponentsAsync(
        string componentName,
        RelationshipType relationshipType = RelationshipType.All,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fields to search within.
/// </summary>
[Flags]
public enum SearchFields
{
    Name = 1,
    Description = 2,
    Parameters = 4,
    Examples = 8,
    All = Name | Description | Parameters | Examples
}

/// <summary>
/// Types of component relationships.
/// </summary>
public enum RelationshipType
{
    All,
    Parent,
    Child,
    Sibling,
    CommonlyUsedWith
}
