// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudBlazor.Mcp.Cli;

namespace MudBlazor.Mcp.Tests.Cli;

public class CliArgsTests
{
    private readonly IReadOnlySet<string> _valueOptions = new HashSet<string> { "--limit", "--in" };
    private readonly IReadOnlySet<string> _flagOptions = new HashSet<string> { "--desc", "--no-inherited" };

    [Fact]
    public void Parse_ValueOptionSpaceAndEqualsForms()
    {
        var spaceForm = CliArgs.Parse(["--limit", "5"], _valueOptions, _flagOptions);
        var equalsForm = CliArgs.Parse(["--limit=5"], _valueOptions, _flagOptions);

        Assert.Equal("5", spaceForm.GetValue("--limit"));
        Assert.Equal("5", equalsForm.GetValue("--limit"));
    }

    [Fact]
    public void Parse_FlagOption()
    {
        var args = CliArgs.Parse(["--desc"], _valueOptions, _flagOptions);

        Assert.True(args.HasFlag("--desc"));
        Assert.Null(args.GetValue("--desc"));
    }

    [Fact]
    public void Parse_PositionalsKeepOrder()
    {
        var args = CliArgs.Parse(["MudButton", "--desc", "extra"], _valueOptions, _flagOptions);

        string[] expected = ["MudButton", "extra"];
        Assert.Equal(expected, args.Positionals);
    }

    [Fact]
    public void Parse_UnknownOption_ThrowsUsage()
    {
        var ex = Assert.Throws<CliException>(() => CliArgs.Parse(["--bogus"], _valueOptions, _flagOptions));
        Assert.Equal(2, ex.ExitCode);
    }

    [Fact]
    public void Parse_SingleDashToken_ThrowsUsage()
    {
        var ex = Assert.Throws<CliException>(() => CliArgs.Parse(["-x"], _valueOptions, _flagOptions));
        Assert.Equal(2, ex.ExitCode);
    }

    [Fact]
    public void Parse_MissingValue_ThrowsUsage()
    {
        var missingValue = Assert.Throws<CliException>(() => CliArgs.Parse(["--limit"], _valueOptions, _flagOptions));
        Assert.Equal(2, missingValue.ExitCode);

        var optionAsValue = Assert.Throws<CliException>(() => CliArgs.Parse(["--limit", "--desc"], _valueOptions, _flagOptions));
        Assert.Equal(2, optionAsValue.ExitCode);
    }

    [Fact]
    public void Parse_FlagWithValue_ThrowsUsage()
    {
        var ex = Assert.Throws<CliException>(() => CliArgs.Parse(["--desc=1"], _valueOptions, _flagOptions));
        Assert.Equal(2, ex.ExitCode);
    }

    [Fact]
    public void Parse_RepeatedOption_ThrowsUsage()
    {
        var ex = Assert.Throws<CliException>(() => CliArgs.Parse(["--limit", "5", "--limit", "6"], _valueOptions, _flagOptions));
        Assert.Equal(2, ex.ExitCode);
    }
}
