// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudDocs.Services;

namespace MudDocs.Tests.Services;

/// <summary>
/// A controllable <see cref="TimeProvider"/> for deterministic testing without Thread.Sleep.
/// </summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeTimeProvider(DateTimeOffset startTime) => _utcNow = startTime;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan delta) => _utcNow += delta;
}

public class VersionCacheManagerTests : IDisposable
{
    private readonly string _testDataPath;
    private readonly FakeTimeProvider _timeProvider;
    private readonly VersionCacheManager _manager;

    public VersionCacheManagerTests()
    {
        _testDataPath = Path.Combine(Path.GetTempPath(), $"muddocs-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDataPath);
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _manager = new VersionCacheManager(_testDataPath, maxVersions: 3, timeProvider: _timeProvider);
    }

    [Fact]
    public void IsVersionCached_ReturnsFalse_WhenNoVersionsExist()
    {
        Assert.False(_manager.IsVersionCached("9.0.0"));
    }

    [Fact]
    public void RegisterVersion_AddsVersion()
    {
        _manager.RegisterVersion("9.0.0");
        Assert.True(_manager.IsVersionCached("9.0.0"));
    }

    [Fact]
    public void TouchVersion_UpdatesLastUsed()
    {
        _manager.RegisterVersion("9.0.0");
        var before = _manager.GetLastUsed("9.0.0");
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        _manager.TouchVersion("9.0.0");
        var after = _manager.GetLastUsed("9.0.0");
        Assert.True(after > before);
    }

    [Fact]
    public void EvictToMakeRoomForNewVersion_RemovesOldestWhenAtCapacity()
    {
        _manager.RegisterVersion("7.0.0");
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        _manager.RegisterVersion("8.0.0");
        _timeProvider.Advance(TimeSpan.FromSeconds(1));
        _manager.RegisterVersion("9.0.0");

        // Create fake directories so eviction can delete them
        Directory.CreateDirectory(Path.Combine(_testDataPath, "v7.0.0"));
        Directory.CreateDirectory(Path.Combine(_testDataPath, "v8.0.0"));
        Directory.CreateDirectory(Path.Combine(_testDataPath, "v9.0.0"));

        var result = _manager.EvictToMakeRoomForNewVersion();
        Assert.Equal(EvictionStatus.Evicted, result.Status);
        Assert.Equal("7.0.0", result.EvictedVersion);
        Assert.False(_manager.IsVersionCached("7.0.0"));
        Assert.False(Directory.Exists(Path.Combine(_testDataPath, "v7.0.0")));
    }

    [Fact]
    public void EvictToMakeRoomForNewVersion_ReturnsNotNeeded_WhenUnderCapacity()
    {
        _manager.RegisterVersion("9.0.0");
        var result = _manager.EvictToMakeRoomForNewVersion();
        Assert.Equal(EvictionStatus.NotNeeded, result.Status);
        Assert.Null(result.EvictedVersion);
    }

    [Fact]
    public void Constructor_ThrowsForZeroMaxVersions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VersionCacheManager(_testDataPath, maxVersions: 0));
    }

    [Fact]
    public void Constructor_ThrowsForNegativeMaxVersions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new VersionCacheManager(_testDataPath, maxVersions: -1));
    }

    [Fact]
    public void PersistsAcrossInstances()
    {
        _manager.RegisterVersion("9.0.0");

        var manager2 = new VersionCacheManager(_testDataPath, maxVersions: 3);
        Assert.True(manager2.IsVersionCached("9.0.0"));
    }

    [Fact]
    public void Constructor_SelfHeals_WhenManifestIsCorrupted()
    {
        // Write corrupt JSON to versions.json
        var manifestPath = Path.Combine(_testDataPath, "versions.json");
        File.WriteAllText(manifestPath, "{{not valid json!!");

        // Should not throw — self-heals by returning an empty manifest
        var manager = new VersionCacheManager(_testDataPath, maxVersions: 3);
        Assert.False(manager.IsVersionCached("9.0.0"));

        // Should be able to register a new version (which overwrites the bad file)
        manager.RegisterVersion("9.0.0");
        Assert.True(manager.IsVersionCached("9.0.0"));
    }

    [Fact]
    public void Constructor_RebuildsManifest_WhenCorruptedWithOrphanedDirectories()
    {
        // Create orphaned version directories on disk
        Directory.CreateDirectory(Path.Combine(_testDataPath, "v1.0.0"));
        Directory.CreateDirectory(Path.Combine(_testDataPath, "v2.0.0"));

        // Corrupt the manifest so LoadManifest() returns empty
        File.WriteAllText(Path.Combine(_testDataPath, "versions.json"), "{{broken json");

        // Construct a new manager — it should re-register the orphaned directories
        var manager = new VersionCacheManager(_testDataPath, maxVersions: 3, timeProvider: _timeProvider);

        Assert.True(manager.IsVersionCached("1.0.0"));
        Assert.True(manager.IsVersionCached("2.0.0"));

        // Re-registered orphans should have MinValue LastUsed (first to be evicted)
        Assert.Equal(DateTimeOffset.MinValue, manager.GetLastUsed("1.0.0"));
        Assert.Equal(DateTimeOffset.MinValue, manager.GetLastUsed("2.0.0"));

        // Directories should still exist on disk
        Assert.True(Directory.Exists(Path.Combine(_testDataPath, "v1.0.0")));
        Assert.True(Directory.Exists(Path.Combine(_testDataPath, "v2.0.0")));
    }

    [Fact]
    public void Constructor_PrunesExcessOrphans_WhenOverCapacity()
    {
        // Create 5 orphaned version directories but maxVersions is 3
        for (var i = 1; i <= 5; i++)
            Directory.CreateDirectory(Path.Combine(_testDataPath, $"v{i}.0.0"));

        // Corrupt the manifest
        File.WriteAllText(Path.Combine(_testDataPath, "versions.json"), "{{broken json");

        var manager = new VersionCacheManager(_testDataPath, maxVersions: 3, timeProvider: _timeProvider);

        // Exactly 3 should be tracked in the manifest
        var trackedCount = 0;
        for (var i = 1; i <= 5; i++)
        {
            if (manager.IsVersionCached($"{i}.0.0"))
                trackedCount++;
        }
        Assert.Equal(3, trackedCount);

        // Excess directories (not tracked) should have been deleted from disk
        var remainingDirs = Directory.GetDirectories(_testDataPath, "v*").Length;
        Assert.Equal(3, remainingDirs);
    }

    [Fact]
    public void Constructor_NoOrphans_WhenDataDirectoryIsEmpty()
    {
        // Empty data dir, no manifest — should produce a clean empty manager
        var emptyPath = Path.Combine(Path.GetTempPath(), $"muddocs-test-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyPath);
        try
        {
            var manager = new VersionCacheManager(emptyPath, maxVersions: 3, timeProvider: _timeProvider);
            Assert.False(manager.IsVersionCached("1.0.0"));
        }
        finally
        {
            Directory.Delete(emptyPath, true);
        }
    }

    [Fact]
    public void Constructor_PrunesExcessVersions_WhenMaxCachedVersionsLowered()
    {
        // Build a valid manifest with 5 versions (simulating maxVersions=5 in the past)
        var setup = new VersionCacheManager(_testDataPath, maxVersions: 5, timeProvider: _timeProvider);
        for (var i = 1; i <= 5; i++)
        {
            setup.RegisterVersion($"{i}.0.0");
            Directory.CreateDirectory(Path.Combine(_testDataPath, $"v{i}.0.0"));
            _timeProvider.Advance(TimeSpan.FromSeconds(1));
        }

        // Now create a new manager with a lower limit — should prune the 2 oldest (1.0.0, 2.0.0)
        var manager = new VersionCacheManager(_testDataPath, maxVersions: 3, timeProvider: _timeProvider);

        Assert.False(manager.IsVersionCached("1.0.0"));
        Assert.False(manager.IsVersionCached("2.0.0"));
        Assert.True(manager.IsVersionCached("3.0.0"));
        Assert.True(manager.IsVersionCached("4.0.0"));
        Assert.True(manager.IsVersionCached("5.0.0"));

        // Pruned directories should be deleted from disk
        Assert.False(Directory.Exists(Path.Combine(_testDataPath, "v1.0.0")));
        Assert.False(Directory.Exists(Path.Combine(_testDataPath, "v2.0.0")));
        Assert.True(Directory.Exists(Path.Combine(_testDataPath, "v3.0.0")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, true);
        }
    }
}
