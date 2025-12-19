// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor.Mcp.Configuration;

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Memory-based documentation cache implementation.
/// </summary>
public sealed class DocumentationCache : IDocumentationCache, IDisposable, IAsyncDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DocumentationCache> _logger;
    private readonly CacheOptions _options;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private readonly object _clearLock = new();
    
    private int _hitCount;
    private int _missCount;
    private long _lastClearedTicks;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentationCache"/> class.
    /// </summary>
    /// <param name="cache">The memory cache instance.</param>
    /// <param name="options">The cache configuration options.</param>
    /// <param name="logger">The logger instance.</param>
    public DocumentationCache(
        IMemoryCache cache,
        IOptions<CacheOptions> options,
        ILogger<DocumentationCache> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public T? Get<T>(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_cache.TryGetValue(key, out T? value))
        {
            Interlocked.Increment(ref _hitCount);
            _logger.LogTrace("Cache hit for key: {Key}", key);
            return value;
        }

        Interlocked.Increment(ref _missCount);
        _logger.LogTrace("Cache miss for key: {Key}", key);
        return default;
    }

    /// <inheritdoc />
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_cache.TryGetValue(key, out T? cachedValue))
        {
            Interlocked.Increment(ref _hitCount);
            _logger.LogTrace("Cache hit for key: {Key}", key);
            return cachedValue!;
        }

        Interlocked.Increment(ref _missCount);
        _logger.LogTrace("Cache miss for key: {Key}, creating value", key);

        var value = await factory(cancellationToken).ConfigureAwait(false);
        
        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.AbsoluteExpirationMinutes)
        };

        _cache.Set(key, value, entryOptions);
        _keys.TryAdd(key, 0);

        return value;
    }

    /// <inheritdoc />
    public void Set<T>(string key, T value, TimeSpan? absoluteExpiration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var entryOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(_options.SlidingExpirationMinutes),
            AbsoluteExpirationRelativeToNow = absoluteExpiration 
                ?? TimeSpan.FromMinutes(_options.AbsoluteExpirationMinutes)
        };

        _cache.Set(key, value, entryOptions);
        _keys.TryAdd(key, 0);
        
        _logger.LogTrace("Cache set for key: {Key}", key);
    }

    /// <inheritdoc />
    public void Remove(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ObjectDisposedException.ThrowIf(_disposed, this);

        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        
        _logger.LogTrace("Cache removed key: {Key}", key);
    }

    /// <inheritdoc />
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_clearLock)
        {
            foreach (var key in _keys.Keys)
            {
                _cache.Remove(key);
            }
            
            _keys.Clear();
            Interlocked.Exchange(ref _hitCount, 0);
            Interlocked.Exchange(ref _missCount, 0);
            Interlocked.Exchange(ref _lastClearedTicks, DateTimeOffset.UtcNow.Ticks);
        }
        
        _logger.LogInformation("Cache cleared");
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var lastClearedTicks = Interlocked.Read(ref _lastClearedTicks);
        var lastCleared = lastClearedTicks > 0 ? new DateTimeOffset(lastClearedTicks, TimeSpan.Zero) : (DateTimeOffset?)null;

        return new CacheStatistics(
            ItemCount: _keys.Count,
            EstimatedSizeBytes: EstimateSizeBytes(),
            HitCount: Volatile.Read(ref _hitCount),
            MissCount: Volatile.Read(ref _missCount),
            LastCleared: lastCleared);
    }

    private long EstimateSizeBytes()
    {
        // Rough estimation - actual implementation would track sizes
        return _keys.Count * 10_000; // Assume ~10KB per cached item
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        // IMemoryCache is disposed by DI container
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
