// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudBlazor.Mcp.Configuration;

public sealed record VersionContext(string Version, string DataPath = "./data")
{
    public string Tag => $"v{Version}";
    public string VersionDataPath => Path.Combine(DataPath, $"v{Version}");
    public string RepoPath => Path.Combine(VersionDataPath, "mudblazor-repo");
    public string IndexPath => Path.Combine(VersionDataPath, "index.json");
    public string StorePath => Path.Combine(VersionDataPath, "store");
}
