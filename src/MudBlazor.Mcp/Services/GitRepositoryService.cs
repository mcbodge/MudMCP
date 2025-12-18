// Copyright (c) 2024 MudBlazor.Mcp Contributors
// Licensed under the MIT License.

using LibGit2Sharp;
using Microsoft.Extensions.Options;
using MudBlazor.Mcp.Configuration;

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Service for managing the MudBlazor Git repository using LibGit2Sharp.
/// </summary>
public sealed class GitRepositoryService : IGitRepositoryService, IDisposable
{
    private readonly ILogger<GitRepositoryService> _logger;
    private readonly MudBlazorOptions _options;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private Repository? _repository;

    public GitRepositoryService(
        ILogger<GitRepositoryService> logger,
        IOptions<MudBlazorOptions> options)
    {
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
        await _syncLock.WaitAsync(cancellationToken);
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
                }, cancellationToken);

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
                    // Fast-forward merge
                    var signature = new Signature("MudBlazor.Mcp", "mcp@mudblazor.com", DateTimeOffset.Now);
                    repo.Merge(trackingBranch, signature, new MergeOptions
                    {
                        FastForwardStrategy = FastForwardStrategy.FastForwardOnly
                    });
                }
            }, cancellationToken);

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
        catch (Exception ex)
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
        await _syncLock.WaitAsync(cancellationToken);
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
                await DeleteDirectoryAsync(RepositoryPath, cancellationToken);
            }
        }
        finally
        {
            _syncLock.Release();
        }

        // Re-clone
        await EnsureRepositoryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public string GetPath(string relativePath)
    {
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
                await Task.Delay(delayMs, cancellationToken);
            }
            catch (UnauthorizedAccessException) when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs, cancellationToken);
            }
        }
    }

    public void Dispose()
    {
        _repository?.Dispose();
        _syncLock.Dispose();
    }
}
