// Copyright (c) 2024 MudBlazor.Mcp Contributors
// Licensed under the MIT License.

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor.Mcp.Configuration;

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Memory-based documentation cache implementation.
/// </summary>
public sealed class DocumentationCache : IDocumentationCache, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<DocumentationCache> _logger;
    private readonly CacheOptions _options;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    
    private int _hitCount;
    private int _missCount;
    private DateTimeOffset? _lastCleared;

    public DocumentationCache(
        IMemoryCache cache,
        IOptions<CacheOptions> options,
        ILogger<DocumentationCache> logger)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public T? Get<T>(string key)
    {
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
        if (_cache.TryGetValue(key, out T? cachedValue))
        {
            Interlocked.Increment(ref _hitCount);
            _logger.LogTrace("Cache hit for key: {Key}", key);
            return cachedValue!;
        }

        Interlocked.Increment(ref _missCount);
        _logger.LogTrace("Cache miss for key: {Key}, creating value", key);

        var value = await factory(cancellationToken);
        
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
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        
        _logger.LogTrace("Cache removed key: {Key}", key);
    }

    /// <inheritdoc />
    public void Clear()
    {
        foreach (var key in _keys.Keys)
        {
            _cache.Remove(key);
        }
        
        _keys.Clear();
        _hitCount = 0;
        _missCount = 0;
        _lastCleared = DateTimeOffset.UtcNow;
        
        _logger.LogInformation("Cache cleared");
    }

    /// <inheritdoc />
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics(
            ItemCount: _keys.Count,
            EstimatedSizeBytes: EstimateSizeBytes(),
            HitCount: _hitCount,
            MissCount: _missCount,
            LastCleared: _lastCleared);
    }

    private long EstimateSizeBytes()
    {
        // Rough estimation - actual implementation would track sizes
        return _keys.Count * 10_000; // Assume ~10KB per cached item
    }

    public void Dispose()
    {
        // IMemoryCache is disposed by DI container
    }
}
