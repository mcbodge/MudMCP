// Copyright (c) 2025 MudBlazor.Mcp Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudBlazor.Mcp.Models;

/// <summary>
/// Represents comprehensive information about a MudBlazor component.
/// </summary>
/// <param name="Name">The component type name (e.g., "MudButton").</param>
/// <param name="Namespace">The namespace containing this component.</param>
/// <param name="Summary">A brief description of the component.</param>
/// <param name="Description">Extended description/remarks about the component.</param>
/// <param name="Category">The category this component belongs to.</param>
/// <param name="BaseType">The base type this component inherits from.</param>
/// <param name="Parameters">Component parameters (properties with [Parameter] attribute).</param>
/// <param name="Events">Event callbacks on the component.</param>
/// <param name="Methods">Public methods on the component.</param>
/// <param name="Examples">Code examples for this component.</param>
/// <param name="RelatedComponents">Related components.</param>
/// <param name="DocumentationUrl">URL to the component's documentation page.</param>
/// <param name="SourceUrl">URL to the component's source code.</param>
public sealed record ComponentInfo(
    string Name,
    string Namespace,
    string Summary,
    string? Description,
    string? Category,
    string? BaseType,
    IReadOnlyList<ComponentParameter> Parameters,
    IReadOnlyList<ComponentEvent> Events,
    IReadOnlyList<ComponentMethod> Methods,
    IReadOnlyList<ComponentExample> Examples,
    IReadOnlyList<string> RelatedComponents,
    string? DocumentationUrl,
    string? SourceUrl
)
{
    /// <summary>
    /// The display name for documentation (e.g., "Button" from "MudButton").
    /// </summary>
    public string DisplayName => Name.StartsWith("Mud") ? Name[3..] : Name;

    /// <summary>
    /// The full type name (e.g., "MudBlazor.MudButton").
    /// </summary>
    public string FullName => $"{Namespace}.{Name}";
}

/// <summary>
/// Represents a component parameter (property with [Parameter] attribute).
/// </summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Type">The parameter type as a string.</param>
/// <param name="Description">Description of the parameter.</param>
/// <param name="DefaultValue">The parameter's default value, if known.</param>
/// <param name="IsRequired">Whether this parameter is required.</param>
/// <param name="IsCascading">Whether this parameter is a cascading parameter.</param>
/// <param name="Category">The category this parameter belongs to (e.g., "Appearance", "Behavior").</param>
public sealed record ComponentParameter(
    string Name,
    string Type,
    string? Description,
    string? DefaultValue,
    bool IsRequired,
    bool IsCascading,
    string? Category
);

/// <summary>
/// Represents an event callback on a component.
/// </summary>
/// <param name="Name">The event name.</param>
/// <param name="EventArgsType">The type of event arguments (null for simple EventCallback).</param>
/// <param name="Description">Description of when this event fires.</param>
public sealed record ComponentEvent(
    string Name,
    string? EventArgsType,
    string? Description
)
{
    /// <summary>
    /// The full event callback type.
    /// </summary>
    public string Type => EventArgsType is not null
        ? $"EventCallback<{EventArgsType}>"
        : "EventCallback";
}

/// <summary>
/// Represents a public method on a component.
/// </summary>
/// <param name="Name">The method name.</param>
/// <param name="ReturnType">The return type as a string.</param>
/// <param name="Description">Description of what the method does.</param>
/// <param name="Parameters">Method parameters.</param>
/// <param name="IsAsync">Whether this method is async.</param>
public sealed record ComponentMethod(
    string Name,
    string ReturnType,
    string? Description,
    IReadOnlyList<MethodParameter> Parameters,
    bool IsAsync
);

/// <summary>
/// Represents a method parameter.
/// </summary>
/// <param name="Name">The parameter name.</param>
/// <param name="Type">The parameter type as a string.</param>
/// <param name="Description">Description of the parameter.</param>
/// <param name="DefaultValue">The default value, if any.</param>
public sealed record MethodParameter(
    string Name,
    string Type,
    string? Description,
    string? DefaultValue
);

/// <summary>
/// Represents a code example for a component.
/// </summary>
/// <param name="Name">The example name (derived from file name).</param>
/// <param name="Description">Description of what this example demonstrates.</param>
/// <param name="RazorMarkup">The Razor markup portion of the example.</param>
/// <param name="CSharpCode">The C# code portion of the example.</param>
/// <param name="SourceFile">Path to the source file for this example.</param>
/// <param name="Features">List of features demonstrated by this example.</param>
public sealed record ComponentExample(
    string Name,
    string? Description,
    string? RazorMarkup,
    string? CSharpCode,
    string? SourceFile,
    IReadOnlyList<string> Features
)
{
    /// <summary>
    /// The complete code for this example.
    /// </summary>
    public string Code => string.IsNullOrEmpty(CSharpCode) 
        ? RazorMarkup ?? "" 
        : $"{RazorMarkup}\n\n@code {{\n{CSharpCode}\n}}";

    /// <summary>
    /// Display title for the example.
    /// </summary>
    public string Title => Name;
};

/// <summary>
/// Represents a component category.
/// </summary>
/// <param name="Name">The category name.</param>
/// <param name="Title">Display title for the category.</param>
/// <param name="Description">Description of this category.</param>
/// <param name="ComponentNames">Components in this category.</param>
public sealed record ComponentCategory(
    string Name,
    string? Title,
    string? Description,
    IReadOnlyList<string> ComponentNames
)
{
    /// <summary>
    /// Alias for ComponentNames for backward compatibility.
    /// </summary>
    public IReadOnlyList<string> Components => ComponentNames;
};

/// <summary>
/// Represents API reference for an enum, class, or interface.
/// </summary>
/// <param name="TypeName">The type name.</param>
/// <param name="Namespace">The namespace containing this type.</param>
/// <param name="Summary">Description of this type.</param>
/// <param name="BaseType">The base type if applicable.</param>
/// <param name="Members">For classes/interfaces: the members.</param>
/// <param name="Kind">The kind of type (enum, class, interface, struct). Defaults to "class".</param>
/// <param name="EnumValues">For enums: the enum values.</param>
public sealed record ApiReference(
    string TypeName,
    string? Namespace,
    string? Summary,
    string? BaseType,
    IReadOnlyList<ApiMember>? Members,
    string Kind = "class",
    IReadOnlyList<EnumValue>? EnumValues = null
)
{
    /// <summary>
    /// The simple type name.
    /// </summary>
    public string Name => TypeName;

    /// <summary>
    /// The full type name with namespace.
    /// </summary>
    public string FullName => Namespace is not null ? $"{Namespace}.{TypeName}" : TypeName;
}

/// <summary>
/// Represents an enum value.
/// </summary>
/// <param name="Name">The enum value name.</param>
/// <param name="Value">The numeric value.</param>
/// <param name="Description">Description of this value.</param>
public sealed record EnumValue(
    string Name,
    string? Value,
    string? Description
);

/// <summary>
/// Represents a member of a class or interface.
/// </summary>
/// <param name="Name">The member name.</param>
/// <param name="MemberType">The member kind (Property, Method, Event, Field).</param>
/// <param name="ReturnType">The member return type.</param>
/// <param name="Description">Description of this member.</param>
/// <param name="ParameterSignature">For methods: the formatted parameter list (e.g., "string name, int count").</param>
public sealed record ApiMember(
    string Name,
    string MemberType,
    string ReturnType,
    string? Description,
    string? ParameterSignature = null
);
