// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Tests.Services;

public class DocumentationCacheTests : IDisposable
{
    private readonly IMemoryCache _memoryCache;
    private readonly DocumentationCache _cache;

    public DocumentationCacheTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new CacheOptions
        {
            SlidingExpirationMinutes = 60,
            AbsoluteExpirationMinutes = 1440
        });
        var logger = Mock.Of<ILogger<DocumentationCache>>();
        
        _cache = new DocumentationCache(_memoryCache, options, logger);
    }

    [Fact]
    public void Get_WithCachedValue_ReturnsValue()
    {
        // Arrange
        const string key = "test-key";
        const string expectedValue = "test-value";
        _cache.Set(key, expectedValue);

        // Act
        var result = _cache.Get<string>(key);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public void Get_WithMissingKey_ReturnsDefault()
    {
        // Act
        var result = _cache.Get<string>("non-existent-key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Set_StoresValue()
    {
        // Arrange
        const string key = "test-key";
        const int value = 42;

        // Act
        _cache.Set(key, value);
        var result = _cache.Get<int>(key);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void Remove_RemovesValue()
    {
        // Arrange
        const string key = "test-key";
        _cache.Set(key, "value");

        // Act
        _cache.Remove(key);
        var result = _cache.Get<string>(key);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Clear_RemovesAllValues()
    {
        // Arrange
        _cache.Set("key1", "value1");
        _cache.Set("key2", "value2");
        _cache.Set("key3", "value3");

        // Act
        _cache.Clear();

        // Assert
        Assert.Null(_cache.Get<string>("key1"));
        Assert.Null(_cache.Get<string>("key2"));
        Assert.Null(_cache.Get<string>("key3"));
    }

    [Fact]
    public async Task GetOrCreateAsync_WithCachedValue_ReturnsCachedValue()
    {
        // Arrange
        const string key = "test-key";
        const string cachedValue = "cached-value";
        _cache.Set(key, cachedValue);
        var factoryCalled = false;

        // Act
        var result = await _cache.GetOrCreateAsync(key, ct =>
        {
            factoryCalled = true;
            return Task.FromResult("new-value");
        });

        // Assert
        Assert.Equal(cachedValue, result);
        Assert.False(factoryCalled, "Factory should not be called when value is cached");
    }

    [Fact]
    public async Task GetOrCreateAsync_WithMissingKey_CallsFactory()
    {
        // Arrange
        const string key = "new-key";
        const string expectedValue = "factory-value";
        var factoryCalled = false;

        // Act
        var result = await _cache.GetOrCreateAsync(key, ct =>
        {
            factoryCalled = true;
            return Task.FromResult(expectedValue);
        });

        // Assert
        Assert.Equal(expectedValue, result);
        Assert.True(factoryCalled, "Factory should be called for missing key");
    }

    [Fact]
    public async Task GetOrCreateAsync_CachesFactoryResult()
    {
        // Arrange
        const string key = "new-key";
        var factoryCallCount = 0;

        // Act
        await _cache.GetOrCreateAsync(key, ct =>
        {
            factoryCallCount++;
            return Task.FromResult("value");
        });
        
        await _cache.GetOrCreateAsync(key, ct =>
        {
            factoryCallCount++;
            return Task.FromResult("value");
        });

        // Assert
        Assert.Equal(1, factoryCallCount);
    }

    [Fact]
    public void GetStatistics_ReturnsCorrectStats()
    {
        // Arrange
        _cache.Set("key1", "value1");
        _cache.Set("key2", "value2");
        _ = _cache.Get<string>("key1"); // Hit
        _ = _cache.Get<string>("key2"); // Hit
        _ = _cache.Get<string>("missing"); // Miss

        // Act
        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(2, stats.ItemCount);
        Assert.Equal(2, stats.HitCount);
        Assert.Equal(1, stats.MissCount);
        Assert.True(stats.EstimatedSizeBytes > 0);
    }

    [Fact]
    public void Clear_ResetsStatistics()
    {
        // Arrange
        _cache.Set("key", "value");
        _ = _cache.Get<string>("key"); // Hit
        _ = _cache.Get<string>("missing"); // Miss

        // Act
        _cache.Clear();
        var stats = _cache.GetStatistics();

        // Assert
        Assert.Equal(0, stats.ItemCount);
        Assert.Equal(0, stats.HitCount);
        Assert.Equal(0, stats.MissCount);
        Assert.NotNull(stats.LastCleared);
    }

    [Fact]
    public void Get_ThrowsOnNullOrEmptyKey()
    {
        // Act & Assert - null throws ArgumentNullException, whitespace throws ArgumentException
        Assert.ThrowsAny<ArgumentException>(() => _cache.Get<string>(null!));
        Assert.ThrowsAny<ArgumentException>(() => _cache.Get<string>(""));
        Assert.ThrowsAny<ArgumentException>(() => _cache.Get<string>("   "));
    }

    [Fact]
    public void Set_ThrowsOnNullOrEmptyKey()
    {
        // Act & Assert - null throws ArgumentNullException, whitespace throws ArgumentException
        Assert.ThrowsAny<ArgumentException>(() => _cache.Set(null!, "value"));
        Assert.ThrowsAny<ArgumentException>(() => _cache.Set("", "value"));
        Assert.ThrowsAny<ArgumentException>(() => _cache.Set("   ", "value"));
    }

    [Fact]
    public void Remove_ThrowsOnNullOrEmptyKey()
    {
        // Act & Assert - null throws ArgumentNullException, whitespace throws ArgumentException
        Assert.ThrowsAny<ArgumentException>(() => _cache.Remove(null!));
        Assert.ThrowsAny<ArgumentException>(() => _cache.Remove(""));
        Assert.ThrowsAny<ArgumentException>(() => _cache.Remove("   "));
    }

    [Fact]
    public async Task GetOrCreateAsync_ThrowsOnNullOrEmptyKey()
    {
        // Act & Assert - null throws ArgumentNullException, whitespace throws ArgumentException
        await Assert.ThrowsAnyAsync<ArgumentException>(() => 
            _cache.GetOrCreateAsync<string>(null!, _ => Task.FromResult("value")));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => 
            _cache.GetOrCreateAsync<string>("", _ => Task.FromResult("value")));
    }

    [Fact]
    public async Task GetOrCreateAsync_ThrowsOnNullFactory()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            _cache.GetOrCreateAsync<string>("key", null!));
    }

    [Fact]
    public void Dispose_PreventsSubsequentOperations()
    {
        // Arrange
        _cache.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _cache.Get<string>("key"));
        Assert.Throws<ObjectDisposedException>(() => _cache.Set("key", "value"));
        Assert.Throws<ObjectDisposedException>(() => _cache.Remove("key"));
        Assert.Throws<ObjectDisposedException>(() => _cache.Clear());
        Assert.Throws<ObjectDisposedException>(() => _cache.GetStatistics());
    }

    [Fact]
    public async Task DisposeAsync_PreventsSubsequentOperations()
    {
        // Arrange
        await _cache.DisposeAsync();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => _cache.Get<string>("key"));
    }

    public void Dispose()
    {
        _cache.Dispose();
        _memoryCache.Dispose();
    }
}
