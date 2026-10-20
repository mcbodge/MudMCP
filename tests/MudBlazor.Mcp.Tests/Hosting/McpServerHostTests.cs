// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudBlazor.Mcp.Hosting;

namespace MudBlazor.Mcp.Tests.Hosting;

public class McpServerHostTests
{
    [Fact]
    public void StripCustomArgs_RemovesStdioAndVersionForms()
    {
        // Arrange
        string[] args = ["--stdio", "--version", "9.0.0", "--version=9.0.0", "--urls", "http://localhost:9000"];

        // Act
        var result = McpServerHost.StripCustomArgs(args);

        // Assert
        Assert.DoesNotContain("--stdio", result);
        Assert.DoesNotContain("--version", result);
        Assert.DoesNotContain("9.0.0", result);
        Assert.DoesNotContain("--version=9.0.0", result);
    }

    [Fact]
    public void StripCustomArgs_RemovesTrailingBareVersion()
    {
        // Arrange
        string[] args = ["--urls", "http://localhost:9000", "--version"];

        // Act
        var result = McpServerHost.StripCustomArgs(args);

        // Assert
        string[] expected = ["--urls", "http://localhost:9000"];
        Assert.Equal(expected, result);
    }

    [Fact]
    public void StripCustomArgs_KeepsHostArguments()
    {
        // Arrange
        string[] args = ["--urls", "http://localhost:9000"];

        // Act
        var result = McpServerHost.StripCustomArgs(args);

        // Assert
        Assert.Equal(args, result);
    }

    [Fact]
    public void StripCustomArgs_RemovesHttpFlag()
    {
        // Arrange
        string[] args = ["--http", "--urls", "http://localhost:9000"];

        // Act
        var result = McpServerHost.StripCustomArgs(args);

        // Assert
        string[] expected = ["--urls", "http://localhost:9000"];
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task RunAsync_WithStdioAndHttp_ReturnsOne()
    {
        // Arrange
        var originalError = Console.Error;
        var capturedError = new StringWriter();
        Console.SetError(capturedError);

        try
        {
            // Act
            var exit = await McpServerHost.RunAsync(["--stdio", "--http", "--version", "9.0.0"]);

            // Assert
            Assert.Equal(1, exit);
            Assert.Contains("--stdio and --http cannot be combined", capturedError.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }
}
