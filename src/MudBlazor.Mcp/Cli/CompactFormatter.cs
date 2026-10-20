// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace MudBlazor.Mcp.Cli;

/// <summary>
/// Renders CLI command output in the compact text format (FMT-001 to FMT-008).
/// Additional members are added by later tasks.
/// </summary>
internal static class CompactFormatter
{
    /// <summary>
    /// Renders a single <see cref="IndexStatus"/> as a compact line: version and state, then the
    /// component and enum counts plus build time when a manifest exists, then an <c>index.json</c>
    /// marker when that file is present.
    /// </summary>
    internal static string IndexStatusLine(IndexStatus status)
    {
        var builder = new StringBuilder();
        builder.Append(status.Version).Append(' ').Append(status.State);

        if (status.BuiltAtUtc is { } builtAt)
        {
            builder
                .Append(" components=").Append(status.ComponentCount.ToString(CultureInfo.InvariantCulture))
                .Append(" enums=").Append(status.EnumCount.ToString(CultureInfo.InvariantCulture))
                .Append(" built=").Append(builtAt.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        }

        if (status.HasIndexJson)
        {
            builder.Append(" index.json=present");
        }

        return builder.ToString();
    }
}
