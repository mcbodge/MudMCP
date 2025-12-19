// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MudBlazor.Mcp.Models;

namespace MudBlazor.Mcp.Services.Parsing;

/// <summary>
/// Parses XML documentation comments from C# source files using Roslyn.
/// </summary>
public sealed partial class XmlDocParser
{
    private readonly ILogger<XmlDocParser> _logger;

    public XmlDocParser(ILogger<XmlDocParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a C# source file and extracts component information.
    /// </summary>
    /// <param name="filePath">The path to the C# source file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The parse result, or null if parsing failed.</returns>
    public async Task<ComponentParseResult?> ParseComponentFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {FilePath}", filePath);
            return null;
        }

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            return ParseSourceCode(sourceCode, filePath);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading file: {FilePath}", filePath);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied reading file: {FilePath}", filePath);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to parse file: {FilePath}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Parses C# source code and extracts component information.
    /// </summary>
    /// <param name="sourceCode">The C# source code to parse.</param>
    /// <param name="filePath">The file path for reference.</param>
    /// <returns>The parse result, or null if no public class was found.</returns>
    public ComponentParseResult? ParseSourceCode(string sourceCode, string filePath)
    {
        ArgumentNullException.ThrowIfNull(sourceCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        // Find the main class declaration
        var classDeclaration = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)));

        if (classDeclaration is null)
        {
            _logger.LogDebug("No public class found in: {FilePath}", filePath);
            return null;
        }

        var className = classDeclaration.Identifier.Text;
        var xmlDoc = ExtractXmlDocumentation(classDeclaration);
        var baseType = ExtractBaseType(classDeclaration);
        var parameters = ExtractParameters(classDeclaration);
        var events = ExtractEvents(classDeclaration);
        var methods = ExtractPublicMethods(classDeclaration);

        return new ComponentParseResult
        {
            ClassName = className,
            FilePath = filePath,
            Namespace = ExtractNamespace(root),
            Summary = xmlDoc.Summary,
            Remarks = xmlDoc.Remarks,
            BaseType = baseType,
            Parameters = parameters,
            Events = events,
            Methods = methods
        };
    }

    /// <summary>
    /// Parses an enum type and extracts its values.
    /// </summary>
    /// <param name="filePath">The path to the enum source file.</param>
    /// <returns>The enum parse result, or null if the file doesn't exist or contains no enum.</returns>
    public EnumParseResult? ParseEnumFile(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        if (!File.Exists(filePath))
            return null;

        var sourceCode = File.ReadAllText(filePath);
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var root = syntaxTree.GetRoot();

        var enumDeclaration = root.DescendantNodes()
            .OfType<EnumDeclarationSyntax>()
            .FirstOrDefault();

        if (enumDeclaration is null)
            return null;

        var values = enumDeclaration.Members.Select(m => new EnumValue(
            Name: m.Identifier.Text,
            Value: m.EqualsValue?.Value.ToString(),
            Description: ExtractSummaryFromTrivia(m)
        )).ToList();

        return new EnumParseResult
        {
            EnumName = enumDeclaration.Identifier.Text,
            Namespace = ExtractNamespace(root),
            Values = values,
            Summary = ExtractSummaryFromTrivia(enumDeclaration)
        };
    }

    private XmlDocumentation ExtractXmlDocumentation(MemberDeclarationSyntax member)
    {
        var trivia = member.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (trivia is null)
        {
            return new XmlDocumentation();
        }

        var summary = ExtractXmlElement(trivia, "summary");
        var remarks = ExtractXmlElement(trivia, "remarks");

        return new XmlDocumentation
        {
            Summary = CleanXmlContent(summary),
            Remarks = CleanXmlContent(remarks)
        };
    }

    private string? ExtractSummaryFromTrivia(MemberDeclarationSyntax member)
    {
        var trivia = member.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (trivia is null)
            return null;

        var summary = ExtractXmlElement(trivia, "summary");
        return CleanXmlContent(summary);
    }

    private static string? ExtractXmlElement(DocumentationCommentTriviaSyntax trivia, string elementName)
    {
        var element = trivia.Content
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString() == elementName);

        return element?.Content.ToString();
    }

    private static string? CleanXmlContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        // Remove leading /// and trim
        var cleaned = XmlCommentPrefixRegex().Replace(content, "");
        cleaned = WhitespaceRegex().Replace(cleaned, " ");
        return cleaned.Trim();
    }

    private List<ComponentParameter> ExtractParameters(ClassDeclarationSyntax classDeclaration)
    {
        var parameters = new List<ComponentParameter>();

        foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            // Check for [Parameter] attribute
            var hasParameterAttribute = property.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString() is "Parameter" or "ParameterAttribute"
                    or "CascadingParameter" or "CascadingParameterAttribute");

            if (!hasParameterAttribute)
                continue;

            var xmlDoc = ExtractXmlDocumentation(property);
            var typeName = property.Type.ToString();
            var defaultValue = property.Initializer?.Value.ToString();

            parameters.Add(new ComponentParameter(
                Name: property.Identifier.Text,
                Type: typeName,
                Description: xmlDoc.Summary,
                DefaultValue: defaultValue,
                IsRequired: HasRequiredModifier(property),
                IsCascading: HasCascadingAttribute(property),
                Category: ExtractCategoryFromAttribute(property)
            ));
        }

        return parameters;
    }

    private List<ComponentEvent> ExtractEvents(ClassDeclarationSyntax classDeclaration)
    {
        var events = new List<ComponentEvent>();

        foreach (var property in classDeclaration.Members.OfType<PropertyDeclarationSyntax>())
        {
            var typeName = property.Type.ToString();
            
            // Check if it's an EventCallback
            if (!typeName.StartsWith("EventCallback"))
                continue;

            var hasParameterAttribute = property.AttributeLists
                .SelectMany(al => al.Attributes)
                .Any(a => a.Name.ToString() is "Parameter" or "ParameterAttribute");

            if (!hasParameterAttribute)
                continue;

            var xmlDoc = ExtractXmlDocumentation(property);
            var eventArgsType = ExtractGenericArgument(typeName);

            events.Add(new ComponentEvent(
                Name: property.Identifier.Text,
                EventArgsType: eventArgsType,
                Description: xmlDoc.Summary
            ));
        }

        return events;
    }

    private List<ComponentMethod> ExtractPublicMethods(ClassDeclarationSyntax classDeclaration)
    {
        var methods = new List<ComponentMethod>();

        foreach (var method in classDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            // Only public methods
            if (!method.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword)))
                continue;

            // Skip common overrides and internal methods
            var methodName = method.Identifier.Text;
            if (methodName.StartsWith("_") || 
                methodName is "Dispose" or "DisposeAsync" or "SetParametersAsync" or "OnInitialized" or
                "OnInitializedAsync" or "OnParametersSet" or "OnParametersSetAsync" or
                "OnAfterRender" or "OnAfterRenderAsync" or "ShouldRender" or "BuildRenderTree")
                continue;

            var xmlDoc = ExtractXmlDocumentation(method);
            var methodParams = method.ParameterList.Parameters.Select(p => new MethodParameter(
                Name: p.Identifier.Text,
                Type: p.Type?.ToString() ?? "object",
                Description: null,
                DefaultValue: p.Default?.Value.ToString()
            )).ToList();

            methods.Add(new ComponentMethod(
                Name: methodName,
                ReturnType: method.ReturnType.ToString(),
                Description: xmlDoc.Summary,
                Parameters: methodParams,
                IsAsync: method.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword)) ||
                         method.ReturnType.ToString().Contains("Task")
            ));
        }

        return methods;
    }

    private static string? ExtractBaseType(ClassDeclarationSyntax classDeclaration)
    {
        return classDeclaration.BaseList?.Types.FirstOrDefault()?.Type.ToString();
    }

    private static string? ExtractNamespace(SyntaxNode root)
    {
        // Handle file-scoped namespaces (.NET 6+)
        var fileScopedNamespace = root.DescendantNodes()
            .OfType<FileScopedNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        if (fileScopedNamespace is not null)
            return fileScopedNamespace.Name.ToString();

        // Handle traditional namespaces
        var namespaceDeclaration = root.DescendantNodes()
            .OfType<NamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return namespaceDeclaration?.Name.ToString();
    }

    private static bool HasRequiredModifier(PropertyDeclarationSyntax property)
    {
        // Check for EditorRequired attribute
        return property.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString() is "EditorRequired" or "EditorRequiredAttribute");
    }

    private static bool HasCascadingAttribute(PropertyDeclarationSyntax property)
    {
        return property.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString() is "CascadingParameter" or "CascadingParameterAttribute");
    }

    private static string? ExtractCategoryFromAttribute(PropertyDeclarationSyntax property)
    {
        var categoryAttr = property.AttributeLists
            .SelectMany(al => al.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "Category" or "CategoryAttribute");

        if (categoryAttr?.ArgumentList?.Arguments.FirstOrDefault() is { } arg)
        {
            // Extract the category value from CategoryTypes.xxx
            var value = arg.ToString();
            var match = CategoryTypesRegex().Match(value);
            return match.Success ? match.Groups[1].Value : null;
        }

        return null;
    }

    private static string? ExtractGenericArgument(string typeName)
    {
        var match = GenericArgumentRegex().Match(typeName);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"^\s*///\s?", RegexOptions.Multiline)]
    private static partial Regex XmlCommentPrefixRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"CategoryTypes\.(\w+)")]
    private static partial Regex CategoryTypesRegex();

    [GeneratedRegex(@"<(.+)>")]
    private static partial Regex GenericArgumentRegex();
}

/// <summary>
/// Result of parsing a component file.
/// </summary>
public record ComponentParseResult
{
    public required string ClassName { get; init; }
    public required string FilePath { get; init; }
    public string? Namespace { get; init; }
    public string? Summary { get; init; }
    public string? Remarks { get; init; }
    public string? BaseType { get; init; }
    public List<ComponentParameter> Parameters { get; init; } = [];
    public List<ComponentEvent> Events { get; init; } = [];
    public List<ComponentMethod> Methods { get; init; } = [];
}

/// <summary>
/// Result of parsing an enum file.
/// </summary>
public record EnumParseResult
{
    public required string EnumName { get; init; }
    public string? Namespace { get; init; }
    public string? Summary { get; init; }
    public List<EnumValue> Values { get; init; } = [];
}

/// <summary>
/// Extracted XML documentation.
/// </summary>
internal record XmlDocumentation
{
    public string? Summary { get; init; }
    public string? Remarks { get; init; }
}
