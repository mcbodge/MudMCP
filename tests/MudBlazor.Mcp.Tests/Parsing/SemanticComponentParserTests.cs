// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Logging.Abstractions;
using MudBlazor.Mcp.Services.Parsing;

namespace MudBlazor.Mcp.Tests.Parsing;

public class SemanticComponentParserTests
{
    private readonly SemanticComponentParser _parser =
        new(NullLogger<SemanticComponentParser>.Instance);

    [Fact]
    public void ExtractComponent_MergesInheritedParametersFromBaseChain()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            public abstract class MudComponentBase
            {
                /// <summary>The CSS class.</summary>
                [Parameter] public string? Class { get; set; }
            }
            """,
            """
            namespace MudBlazor;
            /// <summary>A base button.</summary>
            public abstract class MudBaseButton : MudComponentBase
            {
                /// <summary>The URL to navigate to.</summary>
                [Parameter] public string? Href { get; set; }
                /// <summary>Occurs when clicked.</summary>
                [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
            }
            """,
            """
            namespace MudBlazor;
            /// <summary>A button.</summary>
            public partial class MudButton : MudBaseButton
            {
                /// <summary>The button color.</summary>
                [Parameter] public Color Color { get; set; }
            }
            """);

        var result = _parser.ExtractComponent(source, source.TypesByName["MudButton"]);

        Assert.Equal("MudButton", result.ClassName);
        Assert.Equal("MudBaseButton", result.BaseType);

        var color = result.Parameters.Single(p => p.Name == "Color");
        Assert.False(color.IsInherited);
        Assert.Null(color.DeclaringType);

        var href = result.Parameters.Single(p => p.Name == "Href");
        Assert.True(href.IsInherited);
        Assert.Equal("MudBaseButton", href.DeclaringType);
        Assert.Equal("The URL to navigate to.", href.Description);

        var cssClass = result.Parameters.Single(p => p.Name == "Class");
        Assert.True(cssClass.IsInherited);
        Assert.Equal("MudComponentBase", cssClass.DeclaringType);
    }

    [Fact]
    public void ExtractComponent_ExtractsInheritedEventCallbacks()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            public abstract class MudBaseButton
            {
                /// <summary>Occurs when clicked.</summary>
                [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
            }
            """,
            """
            namespace MudBlazor;
            public partial class MudButton : MudBaseButton
            {
            }
            """);

        var result = _parser.ExtractComponent(source, source.TypesByName["MudButton"]);

        var onClick = result.Events.Single(e => e.Name == "OnClick");
        Assert.Equal("MouseEventArgs", onClick.EventArgsType);
        Assert.True(onClick.IsInherited);
        Assert.Equal("MudBaseButton", onClick.DeclaringType);
    }

    [Fact]
    public void ExtractComponent_MergesPartialClassMembers()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            public partial class MudThing
            {
                /// <summary>Property A.</summary>
                [Parameter] public string? A { get; set; }
            }
            """,
            """
            namespace MudBlazor;
            public partial class MudThing
            {
                /// <summary>Property B.</summary>
                [Parameter] public string? B { get; set; }

                /// <summary>Does the thing.</summary>
                public void DoThing() { }
            }
            """);

        var result = _parser.ExtractComponent(source, source.TypesByName["MudThing"]);

        Assert.Contains(result.Parameters, p => p.Name == "A");
        Assert.Contains(result.Parameters, p => p.Name == "B");
        Assert.Contains(result.Methods, m => m.Name == "DoThing");
    }

    [Fact]
    public void ExtractComponent_ResolvesInheritdocFromOverriddenMember()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            public abstract class BaseInput
            {
                /// <summary>The shared value.</summary>
                public virtual string? Value { get; set; }
            }
            """,
            """
            namespace MudBlazor;
            public partial class MudInput : BaseInput
            {
                /// <inheritdoc/>
                [Parameter] public override string? Value { get; set; }
            }
            """);

        var result = _parser.ExtractComponent(source, source.TypesByName["MudInput"]);

        var value = result.Parameters.Single(p => p.Name == "Value");
        Assert.Equal("The shared value.", value.Description);
    }

    [Fact]
    public void ExtractComponent_HandlesMultipleClassesInOneFile()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;

            /// <summary>Alpha component.</summary>
            public partial class MudAlpha
            {
                [Parameter] public int AlphaValue { get; set; }
            }

            /// <summary>Beta component.</summary>
            public partial class MudBeta
            {
                [Parameter] public int BetaValue { get; set; }
            }
            """);

        var alpha = _parser.ExtractComponent(source, source.TypesByName["MudAlpha"]);
        var beta = _parser.ExtractComponent(source, source.TypesByName["MudBeta"]);

        Assert.Equal("Alpha component.", alpha.Summary);
        Assert.Contains(alpha.Parameters, p => p.Name == "AlphaValue");
        Assert.Equal("Beta component.", beta.Summary);
        Assert.Contains(beta.Parameters, p => p.Name == "BetaValue");
    }

    [Fact]
    public void ExtractComponent_ReadsParameterTypeAndDefaultFromSyntax()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            public partial class MudButton
            {
                /// <summary>The color.</summary>
                [Parameter] public Color Color { get; set; } = Color.Default;

                /// <summary>Disabled state.</summary>
                [Parameter, EditorRequired] public bool Disabled { get; set; }
            }
            """);

        var result = _parser.ExtractComponent(source, source.TypesByName["MudButton"]);

        var color = result.Parameters.Single(p => p.Name == "Color");
        Assert.Equal("Color", color.Type);
        Assert.Equal("Color.Default", color.DefaultValue);

        var disabled = result.Parameters.Single(p => p.Name == "Disabled");
        Assert.Equal("bool", disabled.Type);
        Assert.True(disabled.IsRequired);
    }

    [Fact]
    public void ExtractComponent_CleansInlineXmlDocTags()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            /// <summary>Creates a <see href="https://x/button">button</see> element, or a <see cref="MudLink"/> when <c>Href</c> is set.<br/> Done.</summary>
            public partial class MudButton
            {
                /// <summary>The URL. Defaults to <see langword="null"/>.</summary>
                [Parameter] public string? Href { get; set; }
            }
            """);

        var result = _parser.ExtractComponent(source, source.TypesByName["MudButton"]);

        Assert.DoesNotContain("<see", result.Summary);
        Assert.DoesNotContain("<c>", result.Summary);
        Assert.DoesNotContain("<br", result.Summary);
        Assert.Contains("button", result.Summary);   // inner text of <see href>
        Assert.Contains("MudLink", result.Summary);   // cref value
        Assert.Contains("Href", result.Summary);      // <c>Href</c> inner text

        var href = result.Parameters.Single(p => p.Name == "Href");
        Assert.DoesNotContain("<see", href.Description);
        Assert.Contains("null", href.Description);    // <see langword="null"/>
    }

    [Fact]
    public void ExtractComponent_ExtractsParameterCategoryLastSegment()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            public partial class MudButton
            {
                /// <summary>Color.</summary>
                [Parameter]
                [Category(CategoryTypes.Button.Appearance)]
                public Color Color { get; set; }

                /// <summary>Href.</summary>
                [Parameter]
                [Category(CategoryTypes.Button.ClickAction)]
                public string? Href { get; set; }
            }
            """);

        var result = _parser.ExtractComponent(source, source.TypesByName["MudButton"]);

        Assert.Equal("Appearance", result.Parameters.Single(p => p.Name == "Color").Category);
        Assert.Equal("ClickAction", result.Parameters.Single(p => p.Name == "Href").Category);
    }

    [Fact]
    public void ExtractEnums_ReturnsPublicEnumsPreferringSummaryOverDescription()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            using System.ComponentModel;

            /// <summary>The color themes.</summary>
            public enum Color
            {
                /// <summary>The default theme.</summary>
                [Description("default")] Default,
                Primary,
            }

            internal enum Hidden { A, B }
            """);

        var enums = _parser.ExtractEnums(source);

        var color = Assert.Single(enums, e => e.Name == "Color");
        Assert.Equal("The color themes.", color.Summary);
        Assert.Equal(2, color.Values.Count);

        var defaultValue = color.Values.Single(v => v.Name == "Default");
        // The XML summary is preferred over the [Description] attribute.
        Assert.Equal("The default theme.", defaultValue.Description);

        Assert.DoesNotContain(enums, e => e.Name == "Hidden");
    }

    [Fact]
    public void ExtractEnums_FallsBackToDescriptionAttributeWhenNoSummary()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            using System.ComponentModel;

            public enum Severity
            {
                [Description("A normal message.")] Normal,
            }
            """);

        var enums = _parser.ExtractEnums(source);

        var severity = Assert.Single(enums, e => e.Name == "Severity");
        var normal = severity.Values.Single(v => v.Name == "Normal");
        Assert.Equal("A normal message.", normal.Description);
    }

    [Fact]
    public void ExtractEnums_CapturesExplicitNumericValues()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            [System.Flags]
            public enum Sizes
            {
                None = 0,
                Small = 1,
                Large = 2,
            }
            """);

        var enums = _parser.ExtractEnums(source);

        var sizes = Assert.Single(enums, e => e.Name == "Sizes");
        Assert.Equal(1, sizes.Values.Single(v => v.Name == "Small").Value);
        Assert.Equal(2, sizes.Values.Single(v => v.Name == "Large").Value);
    }

    [Fact]
    public void ExtractEnums_CapturesConstantExpressionValues()
    {
        var source = _parser.CompileFromSource(
            """
            namespace MudBlazor;
            [System.Flags]
            public enum Flags
            {
                None = 0,
                A = 1 << 0,
                B = 1 << 1,
                C = 1 << 2,
                High = (1 << 4) | (1 << 5),
                Neg = -2,
            }
            """);

        var enums = _parser.ExtractEnums(source);

        var flags = Assert.Single(enums, e => e.Name == "Flags");
        Assert.Equal(1, flags.Values.Single(v => v.Name == "A").Value);
        Assert.Equal(2, flags.Values.Single(v => v.Name == "B").Value);
        Assert.Equal(4, flags.Values.Single(v => v.Name == "C").Value);
        Assert.Equal(48, flags.Values.Single(v => v.Name == "High").Value); // (1<<4)|(1<<5) = 16|32
        Assert.Equal(-2, flags.Values.Single(v => v.Name == "Neg").Value);
    }
}
