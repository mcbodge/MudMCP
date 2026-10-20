// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudBlazor.Mcp.Configuration;

namespace MudBlazor.Mcp.Tests.Configuration;

public class VersionArgumentTests
{
    [Fact]
    public void Resolve_SpaceForm_ReturnsValue()
    {
        // Arrange
        string[] args = ["--version", "9.0.0"];

        // Act
        var result = VersionArgument.Resolve(args, _ => null);

        // Assert
        Assert.Equal("9.0.0", result);
    }

    [Fact]
    public void Resolve_EqualsForm_ReturnsValue()
    {
        // Arrange
        string[] args = ["--version=9.0.0"];

        // Act
        var result = VersionArgument.Resolve(args, _ => null);

        // Assert
        Assert.Equal("9.0.0", result);
    }

    [Fact]
    public void Resolve_ValueStartingWithDash_UsesEnvironment()
    {
        // Arrange
        string[] args = ["--version", "-x"];

        // Act
        var result = VersionArgument.Resolve(
            args,
            name => name == VersionArgument.EnvironmentVariableName ? "7.1.0" : null);

        // Assert
        Assert.Equal("7.1.0", result);
    }

    [Fact]
    public void Resolve_WhitespaceValue_UsesEnvironment()
    {
        // Arrange
        string[] args = ["--version", "   "];

        // Act
        var result = VersionArgument.Resolve(
            args,
            name => name == VersionArgument.EnvironmentVariableName ? "7.1.0" : null);

        // Assert
        Assert.Equal("7.1.0", result);
    }

    [Fact]
    public void Resolve_NothingSet_ReturnsNull()
    {
        // Arrange
        string[] args = [];

        // Act
        var result = VersionArgument.Resolve(args, _ => null);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("9.0.0")]
    [InlineData("9.0.0-preview.1")]
    public void IsValid_WithValidVersion_ReturnsTrue(string version)
    {
        // Act
        var result = VersionArgument.IsValid(version);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("9.0")]
    [InlineData("v9.0.0")]
    [InlineData("9.0.0/..")]
    [InlineData("")]
    public void IsValid_WithInvalidVersion_ReturnsFalse(string version)
    {
        // Act
        var result = VersionArgument.IsValid(version);

        // Assert
        Assert.False(result);
    }
}
