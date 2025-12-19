// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Service for caching parsed documentation.
/// </summary>
public interface IDocumentationCache
{
    /// <summary>
    /// Gets a value from the cache.
    /// </summary>
    /// <typeparam name="T">The type of value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <returns>The cached value, or default if not found.</returns>
    T? Get<T>(string key);

    /// <summary>
    /// Gets or creates a value in the cache.
    /// </summary>
    /// <typeparam name="T">The type of value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">Factory to create the value if not cached.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cached or newly created value.</returns>
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a value in the cache.
    /// </summary>
    /// <typeparam name="T">The type of value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="absoluteExpiration">Optional absolute expiration.</param>
    void Set<T>(string key, T value, TimeSpan? absoluteExpiration = null);

    /// <summary>
    /// Removes a value from the cache.
    /// </summary>
    /// <param name="key">The cache key.</param>
    void Remove(string key);

    /// <summary>
    /// Clears all cached data.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    CacheStatistics GetStatistics();
}

/// <summary>
/// Cache statistics.
/// </summary>
public record CacheStatistics(
    int ItemCount,
    long EstimatedSizeBytes,
    int HitCount,
    int MissCount,
    DateTimeOffset? LastCleared);
