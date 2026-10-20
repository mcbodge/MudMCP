// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudBlazor.Mcp.Cli;

/// <summary>
/// Parsed command-line arguments: ordered positionals plus named options.
/// Flags map to a null value; value options map to their string value.
/// </summary>
internal sealed record CliArgs(IReadOnlyList<string> Positionals, IReadOnlyDictionary<string, string?> Options)
{
    /// <summary>Returns whether the named option was supplied (flag or value option).</summary>
    public bool HasFlag(string name) => Options.ContainsKey(name);

    /// <summary>Returns the value supplied for the named option, or null when absent or a flag.</summary>
    public string? GetValue(string name) => Options.TryGetValue(name, out var v) ? v : null;

    /// <summary>
    /// Parses <paramref name="tokens"/> into positionals and options. Every parse failure throws
    /// <see cref="CliException"/> with <see cref="CliExitCodes.Usage"/>.
    /// </summary>
    internal static CliArgs Parse(IReadOnlyList<string> tokens, IReadOnlySet<string> valueOptions, IReadOnlySet<string> flagOptions)
    {
        var positionals = new List<string>();
        var options = new Dictionary<string, string?>(StringComparer.Ordinal);

        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var equalsIndex = token.IndexOf('=', StringComparison.Ordinal);
                var hasInlineValue = equalsIndex >= 0;
                var name = hasInlineValue ? token[..equalsIndex] : token;

                var isValueOption = valueOptions.Contains(name);
                var isFlagOption = flagOptions.Contains(name);

                if (!isValueOption && !isFlagOption)
                {
                    throw new CliException(CliExitCodes.Usage, $"unknown option '{name}'");
                }

                if (options.ContainsKey(name))
                {
                    throw new CliException(CliExitCodes.Usage, $"option '{name}' specified more than once");
                }

                if (isValueOption)
                {
                    string value;
                    if (hasInlineValue)
                    {
                        value = token[(equalsIndex + 1)..];
                        if (value.Length == 0)
                        {
                            throw new CliException(CliExitCodes.Usage, $"option '{name}' requires a value");
                        }
                    }
                    else
                    {
                        var next = i + 1 < tokens.Count ? tokens[i + 1] : null;
                        if (string.IsNullOrEmpty(next) || next.StartsWith("--", StringComparison.Ordinal))
                        {
                            throw new CliException(CliExitCodes.Usage, $"option '{name}' requires a value");
                        }

                        value = next;
                        i++;
                    }

                    options[name] = value;
                }
                else
                {
                    if (hasInlineValue)
                    {
                        throw new CliException(CliExitCodes.Usage, $"option '{name}' does not take a value");
                    }

                    options[name] = null;
                }
            }
            else if (token.StartsWith('-'))
            {
                throw new CliException(CliExitCodes.Usage, $"unknown option '{token}'");
            }
            else
            {
                positionals.Add(token);
            }
        }

        return new CliArgs(positionals, options);
    }
}
