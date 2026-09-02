// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;
using MudBlazor.Mcp.Models;

namespace MudBlazor.Mcp.Services.Parsing;

/// <summary>
/// Builds a single <see cref="CSharpCompilation"/> over the versioned MudBlazor source and extracts
/// version-accurate component and enum information from type symbols.
/// </summary>
/// <remarks>
/// The compilation is created without external metadata references: MudBlazor's own base-type chain
/// binds source-to-source, so inheritance merging and <c>&lt;inheritdoc/&gt;</c> resolution work while
/// the chain naturally terminates at the external <c>ComponentBase</c> (which has no source in the clone).
/// Type strings, default values, and attributes are read from each member's declaring syntax so that
/// primitive types (which would otherwise be unresolved without a corelib reference) render correctly.
/// </remarks>
public sealed partial class SemanticComponentParser
{
    private const int MaxInheritdocDepth = 8;

    private static readonly HashSet<string> ExcludedMethodNames = new(StringComparer.Ordinal)
    {
        "Dispose", "DisposeAsync", "SetParametersAsync", "OnInitialized", "OnInitializedAsync",
        "OnParametersSet", "OnParametersSetAsync", "OnAfterRender", "OnAfterRenderAsync",
        "ShouldRender", "BuildRenderTree",
    };

    private readonly ILogger<SemanticComponentParser> _logger;

    public SemanticComponentParser(ILogger<SemanticComponentParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses every <c>.cs</c> file under <c>src/MudBlazor</c> into a single reference-free compilation.
    /// </summary>
    /// <param name="repositoryPath">The root path of the cloned MudBlazor repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<CompiledSource> CompileAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        var sourceRoot = Path.Combine(repositoryPath, "src", "MudBlazor");
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"MudBlazor source directory not found: {sourceRoot}");
        }

        var files = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsBuildArtifact(f))
            .ToList();

        var parseOptions = new CSharpParseOptions(
            languageVersion: LanguageVersion.Latest,
            documentationMode: DocumentationMode.Parse);

        var trees = new ConcurrentBag<SyntaxTree>();
        await Parallel.ForEachAsync(
            files,
            cancellationToken,
            async (file, ct) =>
            {
                try
                {
                    var text = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    trees.Add(CSharpSyntaxTree.ParseText(text, parseOptions, path: file, cancellationToken: ct));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _logger.LogWarning(ex, "Failed to read source file for compilation: {File}", file);
                }
            }).ConfigureAwait(false);

        // No metadata references: source-to-source binding is sufficient for the MudBlazor base chain.
        var compiledSource = BuildCompiledSource(trees);

        _logger.LogInformation(
            "Built MudBlazor source compilation from {FileCount} files with {TypeCount} types",
            files.Count, compiledSource.AllTypes.Count);

        return compiledSource;
    }

    /// <summary>
    /// Builds a compilation directly from source strings. Intended for unit testing the extraction logic
    /// without touching the file system.
    /// </summary>
    public CompiledSource CompileFromSource(params string[] sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        var parseOptions = new CSharpParseOptions(
            languageVersion: LanguageVersion.Latest,
            documentationMode: DocumentationMode.Parse);

        var trees = sources.Select((source, index) =>
            CSharpSyntaxTree.ParseText(source, parseOptions, path: $"Source{index}.cs"));

        return BuildCompiledSource(trees);
    }

    private static CompiledSource BuildCompiledSource(IEnumerable<SyntaxTree> trees)
    {
        // No metadata references: source-to-source binding is sufficient for the MudBlazor base chain.
        var compilation = CSharpCompilation.Create(
            "MudBlazorSource",
            trees,
            references: null,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var allTypes = GetAllTypeSymbols(compilation.GlobalNamespace).ToList();

        var typesByName = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var type in allTypes)
        {
            // First declaration wins; prefer types in the primary "MudBlazor" namespace on collision.
            if (!typesByName.TryGetValue(type.Name, out var existing)
                || (type.ContainingNamespace?.ToDisplayString() == "MudBlazor"
                    && existing.ContainingNamespace?.ToDisplayString() != "MudBlazor"))
            {
                typesByName[type.Name] = type;
            }
        }

        return new CompiledSource(compilation, typesByName, allTypes);
    }

    /// <summary>
    /// Extracts component information from a type symbol, merging <c>[Parameter]</c>/<c>[CascadingParameter]</c>
    /// properties, event callbacks, and public methods from the full source base-type chain.
    /// </summary>
    public ComponentParseResult ExtractComponent(CompiledSource source, INamedTypeSymbol type)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(type);

        var (summary, remarks) = ExtractDoc(source, type, 0);

        return new ComponentParseResult
        {
            ClassName = type.Name,
            FilePath = type.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath ?? type.Name,
            Namespace = type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : null,
            Summary = summary,
            Remarks = remarks,
            BaseType = GetBaseTypeName(type),
            Parameters = ExtractParameters(source, type),
            Events = ExtractEvents(source, type),
            Methods = ExtractMethods(source, type),
        };
    }

    /// <summary>
    /// Extracts all public enums from the compilation, preferring the XML <c>&lt;summary&gt;</c> over
    /// <c>[Description]</c> for each value and capturing explicit numeric values when declared.
    /// </summary>
    public List<EnumInfo> ExtractEnums(CompiledSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var enums = new List<EnumInfo>();

        foreach (var type in source.AllTypes)
        {
            if (type.TypeKind != TypeKind.Enum || type.DeclaredAccessibility != Accessibility.Public)
            {
                continue;
            }

            var enumSyntax = GetSyntax<EnumDeclarationSyntax>(type);
            if (enumSyntax is null)
            {
                continue;
            }

            var values = new List<EnumValueInfo>();
            foreach (var member in enumSyntax.Members)
            {
                var (memberSummary, _) = XmlDocHelper.ExtractSummaryRemarks(member);
                var description = memberSummary ?? ExtractDescriptionAttribute(member.AttributeLists);

                values.Add(new EnumValueInfo(
                    Name: member.Identifier.Text,
                    Value: ParseExplicitValue(member.EqualsValue?.Value),
                    Description: description));
            }

            var (enumTypeSummary, _) = XmlDocHelper.ExtractSummaryRemarks(enumSyntax);

            enums.Add(new EnumInfo(
                Name: type.Name,
                Namespace: type.ContainingNamespace is { IsGlobalNamespace: false } ns ? ns.ToDisplayString() : null,
                Summary: enumTypeSummary,
                Values: values));
        }

        _logger.LogInformation("Extracted {Count} public enums from source compilation", enums.Count);
        return enums;
    }

    private List<ComponentParameter> ExtractParameters(CompiledSource source, INamedTypeSymbol type)
    {
        var parameters = new List<ComponentParameter>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (current, isInherited) in WalkSourceChain(type))
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (GetSyntax<PropertyDeclarationSyntax>(property) is not { } syntax)
                {
                    continue;
                }

                var isParameter = HasAttribute(syntax.AttributeLists, "Parameter", "CascadingParameter");
                if (!isParameter)
                {
                    continue;
                }

                var typeName = syntax.Type.ToString();
                if (typeName.StartsWith("EventCallback", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!seen.Add(property.Name))
                {
                    continue;
                }

                var (memberSummary, _) = ExtractDoc(source, property, 0);
                parameters.Add(new ComponentParameter(
                    Name: property.Name,
                    Type: typeName,
                    Description: memberSummary,
                    DefaultValue: syntax.Initializer?.Value.ToString(),
                    IsRequired: HasAttribute(syntax.AttributeLists, "EditorRequired"),
                    IsCascading: HasAttribute(syntax.AttributeLists, "CascadingParameter"),
                    Category: ExtractCategoryAttribute(syntax.AttributeLists),
                    IsInherited: isInherited,
                    DeclaringType: isInherited ? current.Name : null));
            }
        }

        return parameters;
    }

    private List<ComponentEvent> ExtractEvents(CompiledSource source, INamedTypeSymbol type)
    {
        var events = new List<ComponentEvent>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (current, isInherited) in WalkSourceChain(type))
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (GetSyntax<PropertyDeclarationSyntax>(property) is not { } syntax)
                {
                    continue;
                }

                var typeName = syntax.Type.ToString();
                if (!typeName.StartsWith("EventCallback", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!HasAttribute(syntax.AttributeLists, "Parameter"))
                {
                    continue;
                }

                if (!seen.Add(property.Name))
                {
                    continue;
                }

                var (memberSummary, _) = ExtractDoc(source, property, 0);
                events.Add(new ComponentEvent(
                    Name: property.Name,
                    EventArgsType: ExtractGenericArgument(typeName),
                    Description: memberSummary,
                    IsInherited: isInherited,
                    DeclaringType: isInherited ? current.Name : null));
            }
        }

        return events;
    }

    private List<ComponentMethod> ExtractMethods(CompiledSource source, INamedTypeSymbol type)
    {
        var methods = new List<ComponentMethod>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (current, isInherited) in WalkSourceChain(type))
        {
            foreach (var method in current.GetMembers().OfType<IMethodSymbol>())
            {
                if (method.DeclaredAccessibility != Accessibility.Public || method.MethodKind != MethodKind.Ordinary)
                {
                    continue;
                }

                var methodName = method.Name;
                if (methodName.StartsWith('_') || ExcludedMethodNames.Contains(methodName))
                {
                    continue;
                }

                if (GetSyntax<MethodDeclarationSyntax>(method) is not { } syntax)
                {
                    continue;
                }

                var methodParams = syntax.ParameterList.Parameters.Select(p => new MethodParameter(
                    Name: p.Identifier.Text,
                    Type: p.Type?.ToString() ?? "object",
                    Description: null,
                    DefaultValue: p.Default?.Value.ToString())).ToList();

                var signatureKey = $"{methodName}({string.Join(",", methodParams.Select(p => p.Type))})";
                if (!seen.Add(signatureKey))
                {
                    continue;
                }

                var returnType = syntax.ReturnType.ToString();
                var (memberSummary, _) = ExtractDoc(source, method, 0);
                methods.Add(new ComponentMethod(
                    Name: methodName,
                    ReturnType: returnType,
                    Description: memberSummary,
                    Parameters: methodParams,
                    IsAsync: syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.AsyncKeyword))
                             || returnType.Contains("Task", StringComparison.Ordinal),
                    IsInherited: isInherited,
                    DeclaringType: isInherited ? current.Name : null));
            }
        }

        return methods;
    }

    /// <summary>
    /// Enumerates a type and its source-declared base types, tagging whether each is inherited.
    /// The walk stops at external types (such as <c>ComponentBase</c>) which have no source syntax.
    /// </summary>
    private static IEnumerable<(INamedTypeSymbol Type, bool IsInherited)> WalkSourceChain(INamedTypeSymbol type)
    {
        for (var current = type; current is not null && IsSourceType(current); current = current.BaseType)
        {
            yield return (current, !SymbolEqualityComparer.Default.Equals(current, type));
        }
    }

    private (string? Summary, string? Remarks) ExtractDoc(CompiledSource source, ISymbol symbol, int depth)
    {
        if (GetSyntax<SyntaxNode>(symbol) is not { } syntax)
        {
            return (null, null);
        }

        var (summary, remarks) = XmlDocHelper.ExtractSummaryRemarks(syntax);
        var hasInheritdoc = XmlDocHelper.HasInheritdoc(syntax);

        if (!hasInheritdoc && summary is not null)
        {
            return (summary, remarks);
        }

        if (depth >= MaxInheritdocDepth)
        {
            return (summary, remarks);
        }

        var inheritSource = FindInheritanceSource(source, symbol);
        if (inheritSource is not null)
        {
            var (baseSummary, baseRemarks) = ExtractDoc(source, inheritSource, depth + 1);
            summary ??= baseSummary;
            remarks ??= baseRemarks;
        }

        return (summary, remarks);
    }

    private static ISymbol? FindInheritanceSource(CompiledSource source, ISymbol symbol)
    {
        // Prefer an explicit cref target when the inheritdoc element specifies one.
        if (GetSyntax<SyntaxNode>(symbol) is { } syntax
            && XmlDocHelper.GetInheritdocCref(syntax) is { } cref
            && ResolveCref(source, symbol, cref) is { } crefTarget)
        {
            return crefTarget;
        }

        switch (symbol)
        {
            case IMethodSymbol method:
                if (method.OverriddenMethod is { } overridden && IsSourceType(overridden.ContainingType))
                {
                    return overridden;
                }

                return FindBaseOrInterfaceMember(symbol);

            case IPropertySymbol property:
                if (property.OverriddenProperty is { } overriddenProperty && IsSourceType(overriddenProperty.ContainingType))
                {
                    return overriddenProperty;
                }

                return FindBaseOrInterfaceMember(symbol);

            case INamedTypeSymbol namedType:
                return namedType.BaseType is { } baseType && IsSourceType(baseType) ? baseType : null;

            default:
                return null;
        }
    }

    private static ISymbol? FindBaseOrInterfaceMember(ISymbol symbol)
    {
        var containing = symbol.ContainingType;
        if (containing is null)
        {
            return null;
        }

        for (var baseType = containing.BaseType; baseType is not null && IsSourceType(baseType); baseType = baseType.BaseType)
        {
            var match = baseType.GetMembers(symbol.Name)
                .FirstOrDefault(m => m.Kind == symbol.Kind && m.DeclaringSyntaxReferences.Length > 0);
            if (match is not null)
            {
                return match;
            }
        }

        foreach (var iface in containing.AllInterfaces)
        {
            var match = iface.GetMembers(symbol.Name)
                .FirstOrDefault(m => m.Kind == symbol.Kind && m.DeclaringSyntaxReferences.Length > 0);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static ISymbol? ResolveCref(CompiledSource source, ISymbol symbol, string cref)
    {
        // Best-effort resolution: crefs look like "Type", "Type.Member", or "Member".
        var trimmed = cref.Trim();
        var lastDot = trimmed.LastIndexOf('.');

        if (lastDot < 0)
        {
            // Bare name: try a type first, then a same-named base/interface member.
            if (source.TypesByName.TryGetValue(trimmed, out var typeOnly))
            {
                return typeOnly;
            }

            return FindBaseOrInterfaceMember(symbol);
        }

        var typeName = trimmed[..lastDot];
        var memberName = trimmed[(lastDot + 1)..];
        var simpleTypeName = typeName.Contains('.', StringComparison.Ordinal)
            ? typeName[(typeName.LastIndexOf('.') + 1)..]
            : typeName;

        if (!source.TypesByName.TryGetValue(simpleTypeName, out var declaringType))
        {
            return null;
        }

        for (var current = declaringType; current is not null && IsSourceType(current); current = current.BaseType)
        {
            var match = current.GetMembers(memberName)
                .FirstOrDefault(m => m.DeclaringSyntaxReferences.Length > 0);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static string? GetBaseTypeName(INamedTypeSymbol type)
    {
        var baseType = type.BaseType;
        if (baseType is null)
        {
            return null;
        }

        // Without a corelib reference the implicit base renders as an unresolved "Object"; treat as none.
        return baseType.Name is "Object" or "ValueType" or "Enum" or "" ? null : baseType.Name;
    }

    private static bool IsSourceType(INamedTypeSymbol type)
    {
        return type.DeclaringSyntaxReferences.Length > 0;
    }

    private static bool IsBuildArtifact(string filePath)
    {
        return filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
            || filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<INamedTypeSymbol> GetAllTypeSymbols(INamespaceSymbol namespaceSymbol)
    {
        foreach (var type in namespaceSymbol.GetTypeMembers())
        {
            yield return type;
            foreach (var nested in GetNestedTypeSymbols(type))
            {
                yield return nested;
            }
        }

        foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
        {
            foreach (var type in GetAllTypeSymbols(childNamespace))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypeSymbols(INamedTypeSymbol type)
    {
        foreach (var nested in type.GetTypeMembers())
        {
            yield return nested;
            foreach (var deeper in GetNestedTypeSymbols(nested))
            {
                yield return deeper;
            }
        }
    }

    private static T? GetSyntax<T>(ISymbol symbol)
        where T : SyntaxNode
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is T match)
            {
                return match;
            }
        }

        return null;
    }

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributeLists, params string[] names)
    {
        return attributeLists
            .SelectMany(list => list.Attributes)
            .Any(attribute =>
            {
                var name = attribute.Name.ToString();
                return names.Any(n => name == n || name == $"{n}Attribute");
            });
    }

    private static string? ExtractCategoryAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var categoryAttribute = attributeLists
            .SelectMany(list => list.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "Category" or "CategoryAttribute");

        if (categoryAttribute?.ArgumentList?.Arguments.FirstOrDefault() is { } argument)
        {
            var match = CategoryTypesRegex().Match(argument.ToString());
            return match.Success ? match.Groups[1].Value : null;
        }

        return null;
    }

    private static string? ExtractDescriptionAttribute(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var descriptionAttribute = attributeLists
            .SelectMany(list => list.Attributes)
            .FirstOrDefault(a => a.Name.ToString() is "Description" or "DescriptionAttribute");

        if (descriptionAttribute?.ArgumentList?.Arguments.FirstOrDefault()?.Expression
            is LiteralExpressionSyntax { Token.Value: string value })
        {
            return value;
        }

        return null;
    }

    private static long? ParseExplicitValue(ExpressionSyntax? expression)
    {
        if (expression is LiteralExpressionSyntax { Token.Value: { } tokenValue }
            && tokenValue is int or long or uint or short or byte)
        {
            return Convert.ToInt64(tokenValue, CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static string? ExtractGenericArgument(string typeName)
    {
        var match = GenericArgumentRegex().Match(typeName);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"CategoryTypes\.(\w+)")]
    private static partial Regex CategoryTypesRegex();

    [GeneratedRegex(@"<(.+)>")]
    private static partial Regex GenericArgumentRegex();
}

/// <summary>
/// A compiled view of the versioned MudBlazor source with fast lookup of declared types.
/// </summary>
/// <param name="Compilation">The reference-free compilation over <c>src/MudBlazor</c>.</param>
/// <param name="TypesByName">Map of simple type name to its symbol (generic arity ignored).</param>
/// <param name="AllTypes">All named type symbols declared in the compilation.</param>
public sealed record CompiledSource(
    CSharpCompilation Compilation,
    IReadOnlyDictionary<string, INamedTypeSymbol> TypesByName,
    IReadOnlyList<INamedTypeSymbol> AllTypes);
