// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MudDocs.Services;

public sealed class VersionCacheManager : IVersionCacheManager
{
    private readonly string _dataPath;
    private readonly int _maxVersions;
    private readonly string _manifestPath;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<VersionCacheManager> _logger;
    private VersionManifest _manifest;

    public VersionCacheManager(string dataPath, int maxVersions = 3, TimeProvider? timeProvider = null, ILogger<VersionCacheManager>? logger = null)
    {
        if (maxVersions < 1)
            throw new ArgumentOutOfRangeException(nameof(maxVersions), maxVersions, "Must be at least 1.");

        _dataPath = dataPath;
        _maxVersions = maxVersions;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<VersionCacheManager>.Instance;
        _manifestPath = Path.Combine(dataPath, "versions.json");
        _manifest = LoadManifest();
        PruneExcessVersions();
        ReconcileOrphanedDirectories();
    }

    public bool IsVersionCached(string version)
        => _manifest.Versions.Any(v => string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase));

    public void RegisterVersion(string version)
    {
        if (IsVersionCached(version)) return;
        _manifest.Versions.Add(new VersionEntry(version, $"v{version}", _timeProvider.GetUtcNow()));
        if (!Save())
            _logger.LogWarning("Failed to persist manifest after registering version {Version}; in-memory state is correct but {ManifestPath} may be stale", version, _manifestPath);
    }

    public void TouchVersion(string version)
    {
        var entry = _manifest.Versions.FirstOrDefault(v => string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return;
        entry.LastUsed = _timeProvider.GetUtcNow();
        if (!Save())
            _logger.LogWarning("Failed to persist manifest after touching version {Version}; in-memory state is correct but {ManifestPath} may be stale", version, _manifestPath);
    }

    public DateTimeOffset? GetLastUsed(string version)
        => _manifest.Versions.FirstOrDefault(v => string.Equals(v.Version, version, StringComparison.OrdinalIgnoreCase))?.LastUsed;

    /// <summary>
    /// Evicts the least-recently-used cached version to make room for a new one.
    /// Must be called <b>before</b> registering a new version when the caller intends
    /// to add a version that is not yet tracked by the manifest.
    /// </summary>
    public EvictionResult EvictToMakeRoomForNewVersion()
    {
        if (_manifest.Versions.Count < _maxVersions)
            return new EvictionResult(EvictionStatus.NotNeeded);

        var oldest = _manifest.Versions.OrderBy(v => v.LastUsed).First();

        // Delete the on-disk data first. Only remove from the manifest if
        // deletion succeeds (or the directory doesn't exist) so the manifest
        // stays in sync with what is actually on disk.
        if (!TryDeleteVersionDirectory(oldest.Version))
            return new EvictionResult(EvictionStatus.Failed);

        // Directory is gone; update the manifest to match.
        _manifest.Versions.Remove(oldest);
        if (!Save())
        {
            // The directory is already deleted but we failed to persist the updated
            // manifest. Report failure so callers know eviction was not fully
            // persisted to disk.
            _logger.LogWarning(
                "Evicted version directory for {Version} was deleted but manifest save failed; in-memory state is correct but {ManifestPath} may be stale",
                oldest.Version, _manifestPath);
            return new EvictionResult(EvictionStatus.Failed);
        }

        return new EvictionResult(EvictionStatus.Evicted, oldest.Version);
    }

    /// <summary>
    /// Removes least-recently-used manifest entries (and their on-disk directories)
    /// when the persisted manifest contains more versions than <c>_maxVersions</c>.
    /// This can happen when the configuration is lowered after versions have already
    /// been cached, or when an old manifest persists across upgrades.
    /// </summary>
    private void PruneExcessVersions()
    {
        if (_manifest.Versions.Count <= _maxVersions)
            return;

        var excess = _manifest.Versions.Count - _maxVersions;
        _logger.LogWarning(
            "Manifest contains {Count} versions but MaxCachedVersions is {Max}; pruning {Excess} LRU version(s)",
            _manifest.Versions.Count, _maxVersions, excess);

        var toRemove = _manifest.Versions
            .OrderBy(v => v.LastUsed)
            .Take(excess)
            .ToList();

        foreach (var entry in toRemove)
        {
            if (TryDeleteVersionDirectory(entry.Version))
            {
                _manifest.Versions.Remove(entry);
                _logger.LogWarning("Pruned excess cached version {Version} (LRU)", entry.Version);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to prune excess cached version {Version}; directory could not be deleted and will remain tracked",
                    entry.Version);
            }
        }

        if (!Save())
            _logger.LogWarning(
                "Failed to persist manifest after pruning excess versions; in-memory state is correct but {ManifestPath} may be stale",
                _manifestPath);
    }

    /// <summary>
    /// Scans the data directory for version directories that exist on disk but are
    /// not tracked in the manifest (orphans). This happens when the manifest is
    /// corrupted or deleted while version directories remain.
    /// Orphans within capacity are re-registered with <see cref="DateTimeOffset.MinValue"/>
    /// so they are evicted first via normal LRU. Excess orphans are deleted immediately.
    /// </summary>
    private void ReconcileOrphanedDirectories()
    {
        if (!Directory.Exists(_dataPath))
            return;

        var trackedVersions = new HashSet<string>(_manifest.Versions.Select(v => v.Version), StringComparer.OrdinalIgnoreCase);

        List<string> orphanVersions;
        try
        {
            orphanVersions = Directory.GetDirectories(_dataPath, "v*")
                .Select(dir => Path.GetFileName(dir))
                .Where(name => name.Length > 1 && name.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                .Select(name => name[1..]) // strip the 'v' prefix
                .Where(version => !trackedVersions.Contains(version))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "IO error scanning for orphaned version directories in {Path}", _dataPath);
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission error scanning for orphaned version directories in {Path}", _dataPath);
            return;
        }

        if (orphanVersions.Count == 0)
            return;

        _logger.LogWarning(
            "Found {Count} orphaned version directory(ies) in {Path} not tracked by the manifest: {Versions}",
            orphanVersions.Count, _dataPath, string.Join(", ", orphanVersions));

        foreach (var version in orphanVersions)
        {
            if (_manifest.Versions.Count < _maxVersions)
            {
                // Re-register with MinValue so it is the first candidate for LRU eviction.
                _manifest.Versions.Add(new VersionEntry(version, $"v{version}", DateTimeOffset.MinValue));
                _logger.LogWarning(
                    "Re-registered orphaned version {Version} into the manifest (LastUsed = MinValue)",
                    version);
            }
            else
            {
                // Over capacity — delete the orphan directory.
                if (TryDeleteVersionDirectory(version))
                {
                    _logger.LogWarning(
                        "Deleted orphaned version directory v{Version} (manifest at capacity {Max})",
                        version, _maxVersions);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to delete orphaned version directory v{Version} while manifest at capacity {Max}; directory will remain on disk untracked",
                        version, _maxVersions);
                }
            }
        }

        if (!Save())
            _logger.LogWarning(
                "Failed to persist manifest after orphan reconciliation; in-memory state is correct but {ManifestPath} may be stale",
                _manifestPath);
    }

    /// <summary>
    /// Attempts to delete a version directory, normalizing file attributes first.
    /// Returns <c>true</c> if the directory was deleted or did not exist;
    /// <c>false</c> if deletion failed due to IO or permission errors.
    /// </summary>
    private bool TryDeleteVersionDirectory(string version)
    {
        var versionDir = Path.Combine(_dataPath, $"v{version}");
        try
        {
            if (!Directory.Exists(versionDir))
                return true;

            var rootDir = new DirectoryInfo(versionDir);
            foreach (var entry in rootDir.EnumerateFileSystemInfos("*", SearchOption.AllDirectories))
            {
                entry.Attributes = FileAttributes.Normal;
            }
            rootDir.Attributes = FileAttributes.Normal;
            Directory.Delete(versionDir, true);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "IO error deleting version directory {Path}", versionDir);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission error deleting version directory {Path}", versionDir);
            return false;
        }
    }

    private VersionManifest LoadManifest()
    {
        if (!File.Exists(_manifestPath))
            return new VersionManifest();
        try
        {
            var json = File.ReadAllText(_manifestPath);
            return JsonSerializer.Deserialize<VersionManifest>(json) ?? new VersionManifest();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Corrupted versions manifest at {Path}, starting fresh", _manifestPath);
            return new VersionManifest();
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading versions manifest at {Path}, cannot continue", _manifestPath);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission error reading versions manifest at {Path}, cannot continue", _manifestPath);
            throw;
        }
    }

    private bool Save()
    {
        var tempPath = _manifestPath + ".tmp";
        try
        {
            Directory.CreateDirectory(_dataPath);
            var json = JsonSerializer.Serialize(_manifest, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tempPath, json);

            File.Move(tempPath, _manifestPath, overwrite: true);
            return true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "IO error saving versions manifest to {Path}", _manifestPath);
            TryDeleteTempFile(tempPath);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Permission error saving versions manifest to {Path}", _manifestPath);
            TryDeleteTempFile(tempPath);
            return false;
        }
    }

    private void TryDeleteTempFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temporary manifest file at {Path}", tempPath);
        }
    }
}

internal sealed class VersionManifest
{
    public List<VersionEntry> Versions { get; set; } = [];
}

internal sealed class VersionEntry
{
    public string Version { get; set; }
    public string Tag { get; set; }
    public DateTimeOffset LastUsed { get; set; }

    public VersionEntry() { Version = ""; Tag = ""; }
    public VersionEntry(string version, string tag, DateTimeOffset lastUsed)
    {
        Version = version;
        Tag = tag;
        LastUsed = lastUsed;
    }
}
