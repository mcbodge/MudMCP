// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

namespace MudDocs.Configuration;

internal static partial class VersionArgument
{
    internal const string EnvironmentVariableName = "MUDBLAZOR_VERSION";

    internal static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    internal static string? Resolve(string[] args, Func<string, string?> getEnvironmentVariable)
    {
        var versionIndex = Array.IndexOf(args, "--version");
        string? v = null;
        if (versionIndex >= 0 && versionIndex + 1 < args.Length)
        {
            // Space-separated form: --version X.Y.Z
            var candidate = args[versionIndex + 1];
            if (!string.IsNullOrEmpty(candidate) && candidate[0] != '-')
            {
                v = candidate;
            }
        }
        else
        {
            // Equals form: --version=X.Y.Z
            var equalsArg = args.FirstOrDefault(a => a.StartsWith("--version=", StringComparison.OrdinalIgnoreCase));
            if (equalsArg is not null)
            {
                v = equalsArg["--version=".Length..];
            }
        }

        v = Normalize(v);
        if (v is null)
        {
            v = Normalize(getEnvironmentVariable(EnvironmentVariableName));
        }

        return v;
    }

    internal static bool IsValid(string version) => VersionRegex().IsMatch(version);

    [GeneratedRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$")]
    private static partial Regex VersionRegex();
}
