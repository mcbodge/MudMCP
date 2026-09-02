// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MudBlazor.Mcp.Services.Parsing;

/// <summary>
/// Shared helpers for extracting and cleaning XML documentation comments from C# syntax nodes.
/// </summary>
internal static partial class XmlDocHelper
{
    /// <summary>
    /// Extracts the cleaned <c>&lt;summary&gt;</c> and <c>&lt;remarks&gt;</c> text from a member's
    /// leading documentation comment trivia.
    /// </summary>
    public static (string? Summary, string? Remarks) ExtractSummaryRemarks(SyntaxNode member)
    {
        var trivia = GetDocTrivia(member);
        if (trivia is null)
        {
            return (null, null);
        }

        return (
            Clean(ExtractElement(trivia, "summary")),
            Clean(ExtractElement(trivia, "remarks")));
    }

    /// <summary>
    /// Determines whether a member's documentation comment contains an <c>&lt;inheritdoc/&gt;</c> element.
    /// </summary>
    public static bool HasInheritdoc(SyntaxNode member)
    {
        var trivia = GetDocTrivia(member);
        if (trivia is null)
        {
            return false;
        }

        return trivia.Content.OfType<XmlEmptyElementSyntax>()
                   .Any(e => e.Name.ToString().Equals("inheritdoc", StringComparison.OrdinalIgnoreCase))
               || trivia.Content.OfType<XmlElementSyntax>()
                   .Any(e => e.StartTag.Name.ToString().Equals("inheritdoc", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the <c>cref</c> value of an <c>&lt;inheritdoc cref="..."/&gt;</c> element, if present.
    /// </summary>
    public static string? GetInheritdocCref(SyntaxNode member)
    {
        var trivia = GetDocTrivia(member);
        if (trivia is null)
        {
            return null;
        }

        var attributes = trivia.Content.OfType<XmlEmptyElementSyntax>()
            .Where(e => e.Name.ToString().Equals("inheritdoc", StringComparison.OrdinalIgnoreCase))
            .SelectMany(e => e.Attributes)
            .Concat(trivia.Content.OfType<XmlElementSyntax>()
                .Where(e => e.StartTag.Name.ToString().Equals("inheritdoc", StringComparison.OrdinalIgnoreCase))
                .SelectMany(e => e.StartTag.Attributes));

        var crefAttr = attributes.OfType<XmlCrefAttributeSyntax>().FirstOrDefault();
        return crefAttr?.Cref.ToString();
    }

    private static DocumentationCommentTriviaSyntax? GetDocTrivia(SyntaxNode member)
    {
        return member.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();
    }

    private static string? ExtractElement(DocumentationCommentTriviaSyntax trivia, string elementName)
    {
        var element = trivia.Content
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString() == elementName);

        return element?.Content.ToString();
    }

    /// <summary>
    /// Strips the <c>///</c> comment prefixes and converts inline XML doc elements
    /// (<c>&lt;see&gt;</c>, <c>&lt;c&gt;</c>, <c>&lt;br/&gt;</c>, etc.) to readable plain text.
    /// </summary>
    public static string? Clean(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var cleaned = XmlCommentPrefixRegex().Replace(content, "");

        // Convert inline XML documentation elements to readable text (paired forms before self-closing).
        cleaned = SeeCrefWithTextRegex().Replace(cleaned, "$1");   // <see cref="X">text</see> -> text
        cleaned = SeeHrefWithTextRegex().Replace(cleaned, "$1");   // <see href="url">text</see> -> text
        cleaned = SeeCrefRegex().Replace(cleaned, "$1");           // <see cref="X"/> -> X
        cleaned = SeeLangwordRegex().Replace(cleaned, "$1");       // <see langword="null"/> -> null
        cleaned = SeeHrefRegex().Replace(cleaned, "$1");           // <see href="url"/> -> url
        cleaned = RefNameRegex().Replace(cleaned, "$1");           // <paramref/typeparamref name="X"/> -> X
        cleaned = LineBreakRegex().Replace(cleaned, " ");          // <br/> -> space
        cleaned = RemainingTagRegex().Replace(cleaned, "");        // strip any other tags, keep inner text

        cleaned = System.Net.WebUtility.HtmlDecode(cleaned);
        cleaned = WhitespaceRegex().Replace(cleaned, " ");
        return cleaned.Trim();
    }

    [GeneratedRegex(@"^\s*///\s?", RegexOptions.Multiline)]
    private static partial Regex XmlCommentPrefixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<see\s+cref\s*=\s*""[^""]*""\s*>(.*?)</see>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SeeCrefWithTextRegex();

    [GeneratedRegex(@"<see\s+href\s*=\s*""[^""]*""\s*>(.*?)</see>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex SeeHrefWithTextRegex();

    [GeneratedRegex(@"<see\s+cref\s*=\s*""([^""]*)""\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex SeeCrefRegex();

    [GeneratedRegex(@"<see\s+langword\s*=\s*""([^""]*)""\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex SeeLangwordRegex();

    [GeneratedRegex(@"<see\s+href\s*=\s*""([^""]*)""\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex SeeHrefRegex();

    [GeneratedRegex(@"<(?:paramref|typeparamref)\s+name\s*=\s*""([^""]*)""\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex RefNameRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreakRegex();

    [GeneratedRegex(@"</?[a-zA-Z][^>]*>")]
    private static partial Regex RemainingTagRegex();
}
