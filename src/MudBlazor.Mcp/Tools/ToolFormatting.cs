// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudBlazor.Mcp.Tools;

/// <summary>
/// Shared Markdown formatting helpers for MCP tools.
/// </summary>
internal static class ToolFormatting
{
    /// <summary>
    /// Truncates <paramref name="text"/> to <paramref name="maxLength"/> characters, appending an ellipsis when trimmed.
    /// </summary>
    /// <param name="text">The text to truncate; may be null or empty.</param>
    /// <param name="maxLength">The maximum length of the returned string, including the trailing ellipsis.</param>
    /// <param name="emptyPlaceholder">Value returned when <paramref name="text"/> is null or empty.</param>
    /// <param name="collapseNewlines">When <see langword="true"/>, newlines are flattened to spaces before truncating (useful for Markdown table cells).</param>
    public static string Truncate(string? text, int maxLength, string emptyPlaceholder = "-", bool collapseNewlines = false)
    {
        if (string.IsNullOrEmpty(text))
            return emptyPlaceholder;

        if (collapseNewlines)
            text = text.Replace("\n", " ").Replace("\r", "");

        return text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";
    }
}
