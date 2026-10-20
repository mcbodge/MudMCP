// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudBlazor.Mcp.Cli;

/// <summary>
/// Process exit codes returned by the command-line interface.
/// </summary>
internal static class CliExitCodes
{
    public const int Success = 0;
    public const int Error = 1;
    public const int Usage = 2;
    public const int NotIndexed = 3;
    public const int NotFound = 4;
    public const int Cancelled = 130;
}

/// <summary>
/// Signals a command-line failure that maps to a specific process <see cref="ExitCode"/>.
/// </summary>
internal sealed class CliException(int exitCode, string message, string? hint = null) : Exception(message)
{
    /// <summary>Gets the process exit code associated with this failure.</summary>
    public int ExitCode { get; } = exitCode;

    /// <summary>Gets an optional hint describing how to resolve the failure.</summary>
    public string? Hint { get; } = hint;
}
