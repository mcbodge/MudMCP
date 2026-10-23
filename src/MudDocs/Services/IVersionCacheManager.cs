// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudDocs.Services;

/// <summary>
/// The outcome of <see cref="IVersionCacheManager.EvictToMakeRoomForNewVersion"/>.
/// </summary>
public enum EvictionStatus
{
    /// <summary>The cache is under capacity; no eviction was needed.</summary>
    NotNeeded,
    /// <summary>A version was successfully evicted (directory deleted + manifest updated).</summary>
    Evicted,
    /// <summary>Eviction was needed but failed (IO/permission error); callers should not proceed with caching a new version.</summary>
    Failed
}

/// <summary>
/// Result of an eviction attempt. When <see cref="Status"/> is <see cref="EvictionStatus.Evicted"/>,
/// <see cref="EvictedVersion"/> contains the version string that was removed.
/// </summary>
public sealed record EvictionResult(EvictionStatus Status, string? EvictedVersion = null);

public interface IVersionCacheManager
{
    bool IsVersionCached(string version);
    void RegisterVersion(string version);
    void TouchVersion(string version);
    DateTimeOffset? GetLastUsed(string version);

    /// <summary>
    /// Evicts the least-recently-used cached version to make room for a new one.
    /// Must be called <b>before</b> registering a new version when the caller intends
    /// to add a version that is not yet tracked by the manifest.
    /// </summary>
    /// <returns>
    /// An <see cref="EvictionResult"/> whose <see cref="EvictionResult.Status"/> indicates:
    /// <list type="bullet">
    ///   <item><see cref="EvictionStatus.NotNeeded"/> — cache is under capacity.</item>
    ///   <item><see cref="EvictionStatus.Evicted"/> — a version was removed; <see cref="EvictionResult.EvictedVersion"/> is set.</item>
    ///   <item><see cref="EvictionStatus.Failed"/> — eviction was needed but failed; caller should not cache a new version.</item>
    /// </list>
    /// </returns>
    EvictionResult EvictToMakeRoomForNewVersion();
}
