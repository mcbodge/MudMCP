// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using MudDocs.Configuration;
using MudDocs.Models;
using MudDocs.Services;

namespace MudDocs.Tests.Cli;

/// <summary>
/// Creates a disposable temporary data path and writes versioned index stores for CLI tests.
/// </summary>
internal sealed class CliTestStore : IDisposable
{
    /// <summary>Gets the root data path that holds the <c>v{version}</c> folders.</summary>
    public string DataPath { get; }

    public CliTestStore()
        : this(Path.Combine(Path.GetTempPath(), "muddocs-cli-" + Guid.NewGuid().ToString("N")))
    {
    }

    public CliTestStore(string dataPath)
    {
        DataPath = dataPath;
        Directory.CreateDirectory(DataPath);
    }

    /// <summary>Writes a current store for <paramref name="version"/> (state <c>ready</c>).</summary>
    public Task WriteReady(string version, CancellationToken cancellationToken) =>
        WriteStoreAsync(version, IndexStore.SchemaVersion, cancellationToken);

    /// <summary>Writes a store whose manifest exists but is not current (state <c>stale</c>).</summary>
    public Task WriteStale(string version, CancellationToken cancellationToken) =>
        WriteStoreAsync(version, schemaVersion: 0, cancellationToken);

    /// <summary>Creates the version folder without a store (state <c>not-indexed</c>).</summary>
    public void WriteNotIndexed(string version) =>
        Directory.CreateDirectory(new VersionContext(version, DataPath).VersionDataPath);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DataPath))
            {
                Directory.Delete(DataPath, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            // Best-effort cleanup of the temp directory.
        }
    }

    private Task WriteStoreAsync(string version, int schemaVersion, CancellationToken cancellationToken)
    {
        var components = CreateComponents();
        var enums = CreateEnums();

        var manifest = new StoreManifest(
            SchemaVersion: schemaVersion,
            MudBlazorVersion: version,
            BuiltAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ComponentCount: components.Count,
            EnumCount: enums.Count,
            IncludeInternalComponents: false,
            IncludeDeprecatedComponents: false,
            MaxExamplesPerComponent: 5);

        return IndexStore.WriteAsync(
            new VersionContext(version, DataPath).StorePath, manifest, components, enums, cancellationToken);
    }

    private static IReadOnlyList<ComponentInfo> CreateComponents() =>
    [
        new ComponentInfo(
            Name: "MudButton",
            Namespace: "MudBlazor",
            Summary: "Represents a button for actions, links, and commands.",
            Description: null,
            Category: "Buttons",
            BaseType: "MudBaseButton",
            Parameters:
            [
                new ComponentParameter("Color", "Color", "The color of the button.", "Color.Default", false, false, "Appearance"),
                new ComponentParameter("Size", "Size", null, "Size.Medium", false, false, "Appearance"),
                new ComponentParameter("Variant", "Variant", null, "Variant.Text", false, false, "Appearance"),
                new ComponentParameter("IconColor", "Color", null, "Color.Inherit", false, false, "Appearance"),
                new ComponentParameter("Disabled", "bool", null, null, false, false, "Behavior", IsInherited: true, DeclaringType: "MudBaseButton"),
                new ComponentParameter("Class", "string?", null, null, false, false, "Common", IsInherited: true, DeclaringType: "MudComponentBase"),
            ],
            Events:
            [
                new ComponentEvent("OnClick", "MouseEventArgs", null),
            ],
            Methods:
            [
                new ComponentMethod("FocusAsync", "Task", null, [], IsAsync: true),
            ],
            Examples:
            [
                new ComponentExample(
                    "Filled",
                    null,
                    "    <MudButton Variant=\"Variant.Filled\" Color=\"Color.Primary\">Filled</MudButton>\n    <MudButton Variant=\"Variant.Filled\" Color=\"Color.Secondary\">Secondary</MudButton>",
                    null,
                    "Filled.razor",
                    ["Variant", "Color", "Disabled", "Colors"]),
                new ComponentExample(
                    "Loading",
                    null,
                    "    <MudButton Disabled=\"_processing\" OnClick=\"Process\">Process</MudButton>",
                    "private bool _processing;",
                    "Loading.razor",
                    ["Disabled", "OnClick"]),
            ],
            RelatedComponents: ["MudIconButton"],
            DocumentationUrl: "https://mudblazor.com/components/button",
            SourceUrl: null),
        new ComponentInfo(
            Name: "MudIconButton",
            Namespace: "MudBlazor",
            Summary: "A button that renders a single icon.",
            Description: null,
            Category: "Buttons",
            BaseType: "MudBaseButton",
            Parameters: [],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null),
        new ComponentInfo(
            Name: "MudBaseButton",
            Namespace: "MudBlazor",
            Summary: "The shared base type for button components.",
            Description: null,
            Category: null,
            BaseType: null,
            Parameters: [],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null),
        new ComponentInfo(
            Name: "MudTextField",
            Namespace: "MudBlazor",
            Summary: "A text input with validation and adornments.",
            Description: null,
            Category: "Form & Inputs",
            BaseType: "MudDebouncedInput",
            Parameters:
            [
                new ComponentParameter("Value", "T?", null, null, true, false, null),
                new ComponentParameter("Form", "MudForm?", null, null, false, true, null, IsInherited: true, DeclaringType: "MudFormComponent"),
            ],
            Events: [],
            Methods: [],
            Examples: [],
            RelatedComponents: [],
            DocumentationUrl: null,
            SourceUrl: null),
    ];

    private static IReadOnlyList<EnumInfo> CreateEnums() =>
    [
        new EnumInfo(
            "Color",
            "MudBlazor",
            "The theme color palette.",
            [
                new EnumValueInfo("Default", null, null),
                new EnumValueInfo("Primary", null, null),
                new EnumValueInfo("Secondary", null, null),
            ]),
        new EnumInfo(
            "Size",
            "MudBlazor",
            "The component sizes.",
            [
                new EnumValueInfo("Small", 0, null),
                new EnumValueInfo("Medium", 1, null),
                new EnumValueInfo("Large", 2, null),
            ]),
    ];
}
