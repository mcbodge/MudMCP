// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudBlazor.Mcp.Configuration;

/// <summary>
/// Configuration options for the MudBlazor MCP server.
/// </summary>
public sealed class MudBlazorOptions
{
    /// <summary>
    /// The configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "MudBlazor";

    /// <summary>
    /// Repository configuration options.
    /// </summary>
    public RepositoryOptions Repository { get; set; } = new();

    /// <summary>
    /// Parsing configuration options.
    /// </summary>
    public ParsingOptions Parsing { get; set; } = new();
}

/// <summary>
/// Configuration for the MudBlazor Git repository.
/// </summary>
public sealed class RepositoryOptions
{
    /// <summary>
    /// The URL of the MudBlazor repository.
    /// </summary>
    public string Url { get; set; } = "https://github.com/MudBlazor/MudBlazor.git";

    /// <summary>
    /// Base directory for all per-version cache data (repos, indexes, versions.json).
    /// </summary>
    public string DataPath { get; set; } = "./data";

    /// <summary>
    /// Maximum number of cached repository versions to keep on disk.
    /// </summary>
    public int MaxCachedVersions { get; set; } = 3;
}

/// <summary>
/// Configuration for documentation parsing.
/// </summary>
public sealed class ParsingOptions
{
    /// <summary>
    /// Whether to include internal components in the index.
    /// </summary>
    public bool IncludeInternalComponents { get; set; } = false;

    /// <summary>
    /// Whether to include deprecated components in the index.
    /// </summary>
    public bool IncludeDeprecatedComponents { get; set; } = true;

    /// <summary>
    /// Maximum number of examples to include per component.
    /// </summary>
    public int MaxExamplesPerComponent { get; set; } = 20;
}
