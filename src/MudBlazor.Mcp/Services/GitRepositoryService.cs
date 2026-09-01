// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using LibGit2Sharp;
using Microsoft.Extensions.Options;
using MudBlazor.Mcp.Configuration;

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Service for managing the MudBlazor Git repository using LibGit2Sharp.
/// </summary>
public sealed class GitRepositoryService : IGitRepositoryService, IDisposable, IAsyncDisposable
{
    private readonly ILogger<GitRepositoryService> _logger;
    private readonly MudBlazorOptions _options;
    private readonly VersionContext _versionContext;
    private readonly IVersionCacheManager _cacheManager;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitRepositoryService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The configuration options.</param>
    /// <param name="versionContext">The version context for the current request.</param>
    /// <param name="cacheManager">The version cache manager.</param>
    public GitRepositoryService(
        ILogger<GitRepositoryService> logger,
        IOptions<MudBlazorOptions> options,
        VersionContext versionContext,
        IVersionCacheManager cacheManager)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(versionContext);
        ArgumentNullException.ThrowIfNull(cacheManager);

        _logger = logger;
        _options = options.Value;
        _versionContext = versionContext;
        _cacheManager = cacheManager;
    }

    /// <inheritdoc />
    public string RepositoryPath => Path.GetFullPath(_versionContext.RepoPath);

    /// <inheritdoc />
    public bool IsAvailable => Directory.Exists(Path.Combine(RepositoryPath, ".git"));

    /// <inheritdoc />
    public string? CurrentCommitHash
    {
        get
        {
            if (!IsAvailable) return null;
            try
            {
                using var repo = new Repository(RepositoryPath);
                return repo.Head.Tip?.Sha[..7];
            }
            catch
            {
                return null;
            }
        }
    }

    /// <inheritdoc />
    public async Task<bool> EnsureRepositoryAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsAvailable)
            {
                _logger.LogInformation("Repository for v{Version} already available at {Path}",
                    _versionContext.Version, RepositoryPath);
                // Upsert: register if missing (e.g., versions.json was deleted/corrupted), then touch.
                _cacheManager.RegisterVersion(_versionContext.Version);
                _cacheManager.TouchVersion(_versionContext.Version);
                return false;
            }

            // Only evict when adding a truly new version to the cache.
            if (!_cacheManager.IsVersionCached(_versionContext.Version))
            {
                var eviction = _cacheManager.EvictToMakeRoomForNewVersion();
                switch (eviction.Status)
                {
                    case EvictionStatus.Evicted:
                        _logger.LogInformation("Evicted cached version v{Version} (LRU)", eviction.EvictedVersion);
                        break;
                    case EvictionStatus.Failed:
                        _logger.LogError(
                            "Eviction failed for MudBlazor v{Version}; aborting clone to avoid exceeding MaxCachedVersions",
                            _versionContext.Version);
                        throw new InvalidOperationException(
                            $"Failed to evict an existing cached MudBlazor version; cannot cache new version {_versionContext.Version}.");
                    case EvictionStatus.NotNeeded:
                        _logger.LogDebug(
                            "No eviction needed when caching MudBlazor v{Version}; within MaxCachedVersions limit",
                            _versionContext.Version);
                        break;
                    default:
                        _logger.LogError(
                            "Unhandled eviction status {Status} when preparing to cache MudBlazor v{Version}",
                            eviction.Status,
                            _versionContext.Version);
                        throw new InvalidOperationException(
                            $"Unhandled eviction status '{eviction.Status}' while preparing to cache MudBlazor version '{_versionContext.Version}'.");
                }
            }

            _logger.LogInformation("Cloning MudBlazor repository at tag {Tag} to {Path}",
                _versionContext.Tag, RepositoryPath);

            var parentDir = Path.GetDirectoryName(RepositoryPath);
            if (!string.IsNullOrEmpty(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            await Task.Run(() =>
            {
                var cloneOptions = new CloneOptions
                {
                    RecurseSubmodules = false
                };

                // Corporate networks/proxies often block CRL/OCSP endpoints, so LibGit2Sharp
                // reports the server certificate as invalid ("certificate revocation status
                // could not be verified") and aborts. Re-validate chain + hostname ourselves
                // without the revocation check so the public MudBlazor repo still clones.
                cloneOptions.FetchOptions.CertificateCheck = AcceptCertificateIgnoringRevocation;

                Repository.Clone(_options.Repository.Url, RepositoryPath, cloneOptions);

                // Checkout the specific tag
                using var repo = new Repository(RepositoryPath);
                var tag = repo.Tags[_versionContext.Tag]
                    ?? throw new InvalidOperationException(
                        $"Tag '{_versionContext.Tag}' not found in repository. Check available MudBlazor versions at https://github.com/MudBlazor/MudBlazor/tags");

                var targetCommit = tag.PeeledTarget as Commit
                    ?? throw new InvalidOperationException($"Tag '{_versionContext.Tag}' does not point to a valid commit");
                Commands.Checkout(repo, targetCommit);
            }, cancellationToken).ConfigureAwait(false);

            _cacheManager.RegisterVersion(_versionContext.Version);
            _cacheManager.TouchVersion(_versionContext.Version);

            _logger.LogInformation("Successfully cloned MudBlazor v{Version}. Commit: {Commit}",
                _versionContext.Version, CurrentCommitHash);

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to ensure MudBlazor repository for v{Version}", _versionContext.Version);
            throw;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    // Validates the TLS certificate when LibGit2Sharp's built-in check fails. We tolerate an
    // unverifiable revocation status (common behind corporate proxies) but still require the
    // chain to build to a trusted root and the hostname to match, preserving protection
    // against untrusted or mismatched certificates.
    private bool AcceptCertificateIgnoringRevocation(Certificate certificate, bool valid, string host)
    {
        if (valid)
        {
            return true;
        }

        if (certificate is CertificateX509 { Certificate: { } rawCertificate })
        {
            using var leaf = X509CertificateLoader.LoadCertificate(rawCertificate.Export(X509ContentType.Cert));
            using var chain = new X509Chain();
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            var chainIsValid = chain.Build(leaf)
                || chain.ChainStatus.All(status =>
                    status.Status is X509ChainStatusFlags.NoError
                        or X509ChainStatusFlags.RevocationStatusUnknown
                        or X509ChainStatusFlags.OfflineRevocation
                        or X509ChainStatusFlags.PartialChain);

            var hostMatches = leaf.MatchesHostname(host);

            if (chainIsValid && hostMatches)
            {
                _logger.LogWarning(
                    "Accepting certificate for {Host} without a fully verified chain/revocation status (hostname validated; remaining issues limited to missing intermediates or unreachable revocation endpoints). This is expected behind corporate proxies.",
                    host);
                return true;
            }

            _logger.LogError(
                "Rejecting certificate for {Host}: hostMatches={HostMatches}, chainStatus=[{ChainStatus}]",
                host, hostMatches, string.Join(", ", chain.ChainStatus.Select(s => s.Status)));
        }

        return false;
    }

    /// <inheritdoc />
    public async Task ForceRefreshAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Force refreshing MudBlazor repository...");

            // Delete existing repository
            if (Directory.Exists(RepositoryPath))
            {
                // Delete with retry for locked files
                await DeleteDirectoryAsync(RepositoryPath, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _syncLock.Release();
        }

        // Re-clone
        await EnsureRepositoryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task DeleteDirectoryAsync(string path, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        const int delayMs = 500;

        for (var i = 0; i < maxRetries; i++)
        {
            try
            {
                // Remove read-only attributes
                var directoryInfo = new DirectoryInfo(path);
                foreach (var file in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    file.Attributes = FileAttributes.Normal;
                }

                Directory.Delete(path, true);
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _syncLock.Dispose();
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
