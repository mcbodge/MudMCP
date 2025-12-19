// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

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
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private Repository? _repository;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitRepositoryService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="options">The configuration options.</param>
    public GitRepositoryService(
        ILogger<GitRepositoryService> logger,
        IOptions<MudBlazorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public string RepositoryPath => Path.GetFullPath(_options.Repository.LocalPath);

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
            if (!IsAvailable)
            {
                _logger.LogInformation("Cloning MudBlazor repository from {Url} to {Path}",
                    _options.Repository.Url, RepositoryPath);

                // Ensure parent directory exists
                var parentDir = Path.GetDirectoryName(RepositoryPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                // Clone the repository
                await Task.Run(() =>
                {
                    var cloneOptions = new CloneOptions
                    {
                        BranchName = _options.Repository.Branch,
                        RecurseSubmodules = false
                    };

                    Repository.Clone(_options.Repository.Url, RepositoryPath, cloneOptions);
                }, cancellationToken).ConfigureAwait(false);

                _logger.LogInformation("Successfully cloned MudBlazor repository. Commit: {Commit}",
                    CurrentCommitHash);

                return true;
            }

            // Repository exists, try to pull latest changes
            _logger.LogInformation("Updating MudBlazor repository...");

            var previousCommit = CurrentCommitHash;

            await Task.Run(() =>
            {
                using var repo = new Repository(RepositoryPath);

                // Fetch latest changes
                var remote = repo.Network.Remotes["origin"];
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

                Commands.Fetch(repo, remote.Name, refSpecs, new FetchOptions(), null);

                // Get the tracking branch
                var trackingBranch = repo.Head.TrackedBranch;
                if (trackingBranch != null)
                {
                    // Hard reset to the remote branch to avoid conflicts
                    // This is safe since we only read from the repository
                    repo.Reset(ResetMode.Hard, trackingBranch.Tip);
                }
            }, cancellationToken).ConfigureAwait(false);

            var currentCommit = CurrentCommitHash;
            var wasUpdated = previousCommit != currentCommit;

            if (wasUpdated)
            {
                _logger.LogInformation("Repository updated from {Previous} to {Current}",
                    previousCommit, currentCommit);
            }
            else
            {
                _logger.LogDebug("Repository already up to date at {Commit}", currentCommit);
            }

            return wasUpdated;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error while ensuring MudBlazor repository");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied while ensuring MudBlazor repository");
            throw;
        }
        catch (LibGit2Sharp.LibGit2SharpException ex)
        {
            _logger.LogError(ex, "Git operation failed while ensuring MudBlazor repository");
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to ensure MudBlazor repository");
            throw;
        }
        finally
        {
            _syncLock.Release();
        }
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
                // Dispose any open repository handles
                _repository?.Dispose();
                _repository = null;

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

    /// <inheritdoc />
    public string GetPath(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        return Path.Combine(RepositoryPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
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
        _repository?.Dispose();
        _syncLock.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        
        await _syncLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _repository?.Dispose();
        }
        finally
        {
            _syncLock.Release();
            _syncLock.Dispose();
        }
    }
}
