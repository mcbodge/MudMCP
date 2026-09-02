// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Services.Parsing;

namespace MudBlazor.Mcp.Tests.Parsing;

public class RazorDocParserTests
{
    private readonly RazorDocParser _parser = new(NullLogger<RazorDocParser>.Instance);

    [Fact]
    public void ParseDocumentation_WithSubTitle_ReturnsDescription()
    {
        var content = """
            @page "/components/button"
            <DocsPage>
                <DocsPageHeader Title="Button" SubTitle="A clickable button for actions." />
            </DocsPage>
            """;

        var result = _parser.ParseDocumentation(content, "ButtonPage.razor");

        Assert.NotNull(result);
        Assert.Equal("MudButton", result.ComponentName);
        Assert.Equal("A clickable button for actions.", result.Description);
    }

    [Fact]
    public void ParseDocumentation_WithoutSubTitle_DoesNotGrabStrayMudText()
    {
        // Regression: a bare <MudText> (e.g. a "Note" callout or nav label) must NOT
        // become the component description; the source XML summary is authoritative instead.
        var content = """
            @page "/components/alert"
            <DocsPage>
                <DocsPageHeader Title="Alert" Component="@nameof(MudAlert)" />
                <MudText>Note</MudText>
            </DocsPage>
            """;

        var result = _parser.ParseDocumentation(content, "AlertPage.razor");

        Assert.NotNull(result);
        Assert.Null(result.Description);
    }
}
