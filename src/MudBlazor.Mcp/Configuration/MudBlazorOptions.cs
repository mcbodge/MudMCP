// Copyright (c) 2025 MudBlazor.Mcp Contributors
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
    /// Cache configuration options.
    /// </summary>
    public CacheOptions Cache { get; set; } = new();

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
    /// The branch to clone/pull.
    /// </summary>
    public string Branch { get; set; } = "dev";

    /// <summary>
    /// Local path where the repository is cloned.
    /// </summary>
    public string LocalPath { get; set; } = "./data/mudblazor-repo";
}

/// <summary>
/// Configuration for documentation caching.
/// </summary>
public sealed class CacheOptions
{
    /// <summary>
    /// Interval in minutes between automatic cache refreshes.
    /// </summary>
    public int RefreshIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Duration in minutes to cache component documentation.
    /// </summary>
    public int ComponentCacheDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Duration in minutes to cache code examples.
    /// </summary>
    public int ExampleCacheDurationMinutes { get; set; } = 120;

    /// <summary>
    /// Sliding expiration in minutes - cache expires if not accessed within this time.
    /// </summary>
    public int SlidingExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Absolute expiration in minutes - cache expires after this time regardless of access.
    /// </summary>
    public int AbsoluteExpirationMinutes { get; set; } = 1440; // 24 hours
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
