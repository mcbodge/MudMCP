// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Service for managing the MudBlazor Git repository.
/// </summary>
public interface IGitRepositoryService
{
    /// <summary>
    /// Gets the local path to the repository.
    /// </summary>
    string RepositoryPath { get; }

    /// <summary>
    /// Gets whether the repository is available locally.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the current commit hash of the repository.
    /// </summary>
    string? CurrentCommitHash { get; }

    /// <summary>
    /// Clones or updates the MudBlazor repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the repository was updated, false if it was already up to date.</returns>
    Task<bool> EnsureRepositoryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a fresh clone of the repository.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ForceRefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the path to a specific directory within the repository.
    /// </summary>
    /// <param name="relativePath">Relative path from repository root.</param>
    /// <returns>Full path to the directory.</returns>
    string GetPath(string relativePath);
}
