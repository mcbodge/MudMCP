// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace MudBlazor.Mcp.Services.Parsing;

/// <summary>
/// Derives component categories from the versioned MudBlazor documentation menu
/// (<c>src/MudBlazor.Docs/Services/Menu/MenuService.cs</c>) by syntax-walking the
/// <c>_docsComponents</c> fluent chain.
/// </summary>
/// <remarks>
/// Named <c>AddNavGroup</c> groups (for example, "Form &amp; Inputs", "Pickers", "Buttons", "Charts",
/// "Functional") map their items to the group name. Ungrouped top-level <c>AddItem</c> entries map to
/// the default "Components" category. Components absent from the menu resolve to <see langword="null"/>.
/// </remarks>
public sealed class MenuCategoryParser
{
    /// <summary>The category assigned to ungrouped top-level menu items.</summary>
    public const string DefaultCategory = "Components";

    private const string DocsComponentsFieldName = "_docsComponents";

    private readonly ILogger<MenuCategoryParser> _logger;
    private readonly Dictionary<string, string> _typeToCategory = new(StringComparer.OrdinalIgnoreCase);
    private bool _isInitialized;

    public MenuCategoryParser(ILogger<MenuCategoryParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses the MudBlazor menu definition to build the component-to-category map.
    /// </summary>
    /// <param name="repositoryPath">The root path of the cloned MudBlazor repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);

        if (_isInitialized)
        {
            return;
        }

        var menuPath = Path.Combine(repositoryPath, "src", "MudBlazor.Docs", "Services", "Menu", "MenuService.cs");
        if (!File.Exists(menuPath))
        {
            _logger.LogWarning("MenuService.cs not found at {Path}; component categories will be unavailable", menuPath);
            _isInitialized = true;
            return;
        }

        var source = await File.ReadAllTextAsync(menuPath, cancellationToken).ConfigureAwait(false);

        _typeToCategory.Clear();
        foreach (var (typeName, category) in ParseMenuSource(source))
        {
            _typeToCategory[typeName] = category;
        }

        _isInitialized = true;
        _logger.LogInformation("Parsed {Count} component-to-category mappings from MenuService", _typeToCategory.Count);
    }

    /// <summary>
    /// Gets the menu-derived category for a component, or <see langword="null"/> if it is not listed in the menu.
    /// </summary>
    /// <param name="componentName">The component type name (e.g., "MudButton").</param>
    public string? GetCategoryName(string componentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(componentName);
        return _typeToCategory.GetValueOrDefault(componentName);
    }

    /// <summary>
    /// Parses a <c>MenuService.cs</c> source string into a map of component type name to category name.
    /// </summary>
    /// <param name="source">The C# source of MenuService.cs.</param>
    public static IReadOnlyDictionary<string, string> ParseMenuSource(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var root = CSharpSyntaxTree.ParseText(source).GetRoot();

        var initializer = root.DescendantNodes()
            .OfType<FieldDeclarationSyntax>()
            .FirstOrDefault(f => f.Declaration.Variables.Any(v => v.Identifier.Text == DocsComponentsFieldName))
            ?.Declaration.Variables.First().Initializer?.Value;

        if (initializer is null)
        {
            return map;
        }

        foreach (var invocation in initializer.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            if (GetInvokedMethodName(invocation) != "AddItem")
            {
                continue;
            }

            var category = FindEnclosingGroup(invocation) ?? DefaultCategory;

            foreach (var typeOf in invocation.ArgumentList.Arguments
                         .Select(a => a.Expression)
                         .OfType<TypeOfExpressionSyntax>())
            {
                var typeName = GetSimpleTypeName(typeOf.Type);
                if (!string.IsNullOrEmpty(typeName))
                {
                    // First mapping wins so a type keeps its own item's category over any later reference.
                    map.TryAdd(typeName, category);
                }
            }
        }

        return map;
    }

    private static string? FindEnclosingGroup(SyntaxNode addItemInvocation)
    {
        foreach (var ancestor in addItemInvocation.Ancestors())
        {
            if (ancestor is ArgumentListSyntax argumentList
                && argumentList.Parent is InvocationExpressionSyntax navGroupInvocation
                && GetInvokedMethodName(navGroupInvocation) == "AddNavGroup")
            {
                return GetFirstStringArgument(navGroupInvocation);
            }
        }

        return null;
    }

    private static string? GetInvokedMethodName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            _ => null,
        };
    }

    private static string? GetFirstStringArgument(InvocationExpressionSyntax invocation)
    {
        return invocation.ArgumentList.Arguments
            .Select(a => a.Expression)
            .OfType<LiteralExpressionSyntax>()
            .FirstOrDefault(l => l.IsKind(SyntaxKind.StringLiteralExpression))
            ?.Token.ValueText;
    }

    private static string GetSimpleTypeName(TypeSyntax type)
    {
        return type switch
        {
            GenericNameSyntax generic => generic.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            QualifiedNameSyntax qualified => GetSimpleTypeName(qualified.Right),
            _ => type.ToString(),
        };
    }
}
