// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using MudDocs.Models;
using MudDocs.Services;

namespace MudDocs.Tests.Services;

public class IndexStoreTests
{
    // Stands in for an over-length (>128 char) name, which cannot be expressed as an InlineData constant.
    private const string OverLongMarker = "__TOOLONG__";

    private static string NewStorePath() =>
        Path.Combine(Path.GetTempPath(), "muddocs-store-" + Guid.NewGuid().ToString("N"), "store");

    private static ComponentInfo CreateComponent(string name) => new(
        Name: name,
        Namespace: "MudBlazor",
        Summary: "A clickable button.",
        Description: "Triggers an action when clicked.",
        Category: "Buttons",
        BaseType: null,
        Parameters: [new ComponentParameter("Color", "Color", "The button color.", "Default", false, false, "Appearance")],
        Events: [],
        Methods: [],
        Examples:
        [
            new ComponentExample(
                "ButtonExample",
                "A basic button.",
                "<MudButton>Click</MudButton>",
                "private void OnClick() { }",
                "ButtonExample.razor",
                ["OnClick"]),
        ],
        RelatedComponents: [],
        DocumentationUrl: null,
        SourceUrl: null);

    private static EnumInfo CreateEnum(string name) => new(
        Name: name,
        Namespace: "MudBlazor",
        Summary: "The theme colors.",
        Values:
        [
            new EnumValueInfo("Primary", 1, "The primary color."),
            new EnumValueInfo("Secondary", 2, "The secondary color."),
        ]);

    private static StoreManifest CreateManifest(
        string version = "8.0.0",
        int componentCount = 1,
        int enumCount = 1,
        bool includeInternal = false) => new(
        SchemaVersion: IndexStore.SchemaVersion,
        MudBlazorVersion: version,
        BuiltAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        ComponentCount: componentCount,
        EnumCount: enumCount,
        IncludeInternalComponents: includeInternal,
        IncludeDeprecatedComponents: false,
        MaxExamplesPerComponent: 5);

    private static void TryDeleteParent(string storePath)
    {
        try
        {
            var parent = Path.GetDirectoryName(storePath);
            if (parent is not null && Directory.Exists(parent))
            {
                Directory.Delete(parent, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup of the temp directory.
        }
    }

    [Fact]
    public async Task WriteAsync_WritesManifestCatalogComponentsAndEnums()
    {
        var storePath = NewStorePath();
        try
        {
            var manifest = CreateManifest(componentCount: 1, enumCount: 1);

            await IndexStore.WriteAsync(
                storePath, manifest, [CreateComponent("MudButton")], [CreateEnum("Color")], TestContext.Current.CancellationToken);

            Assert.True(File.Exists(Path.Combine(storePath, "manifest.json")));
            Assert.True(File.Exists(Path.Combine(storePath, "catalog.json")));
            Assert.True(File.Exists(Path.Combine(storePath, "enums.json")));
            Assert.True(File.Exists(Path.Combine(storePath, "components", "mudbutton.json")));

            var read = new IndexStore(storePath).TryReadManifest();
            Assert.NotNull(read);
            Assert.Equal(1, read.ComponentCount);
            Assert.Equal(1, read.EnumCount);
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }

    [Fact]
    public async Task WriteAsync_CatalogHasNoExampleCode()
    {
        var storePath = NewStorePath();
        try
        {
            await IndexStore.WriteAsync(
                storePath, CreateManifest(), [CreateComponent("MudButton")], [], TestContext.Current.CancellationToken);

            var store = new IndexStore(storePath);

            var full = store.TryReadComponent("MudButton");
            Assert.NotNull(full);
            var fullExample = Assert.Single(full.Examples);
            Assert.NotNull(fullExample.RazorMarkup);
            Assert.NotNull(fullExample.CSharpCode);

            var catalogComponent = Assert.Single(store.ReadCatalog());
            var catalogExample = Assert.Single(catalogComponent.Examples);
            Assert.Null(catalogExample.RazorMarkup);
            Assert.Null(catalogExample.CSharpCode);
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }

    [Fact]
    public async Task WriteAsync_ReplacesExistingStoreAndLeavesNoTempOrOldFolders()
    {
        var storePath = NewStorePath();
        try
        {
            await IndexStore.WriteAsync(
                storePath, CreateManifest(), [CreateComponent("MudButton")], [], TestContext.Current.CancellationToken);
            await IndexStore.WriteAsync(
                storePath, CreateManifest(), [CreateComponent("MudButton")], [], TestContext.Current.CancellationToken);

            Assert.True(Directory.Exists(storePath));

            var parent = Path.GetDirectoryName(storePath)!;
            Assert.Empty(Directory.GetDirectories(parent, "store.tmp-*"));
            Assert.Empty(Directory.GetDirectories(parent, "store.old-*"));
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }

    [Fact]
    public async Task Json_IsCamelCaseAndOmitsDefaults()
    {
        var storePath = NewStorePath();
        try
        {
            await IndexStore.WriteAsync(
                storePath, CreateManifest(includeInternal: false), [CreateComponent("MudButton")], [], TestContext.Current.CancellationToken);

            var json = await File.ReadAllTextAsync(Path.Combine(storePath, "manifest.json"), TestContext.Current.CancellationToken);

            Assert.Contains("\"schemaVersion\"", json);
            Assert.DoesNotContain("\"includeInternalComponents\"", json);
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }

    [Fact]
    public async Task IsCurrent_FalseWhenMissingSchemaMismatchOrOtherVersion()
    {
        var storePath = NewStorePath();
        try
        {
            var store = new IndexStore(storePath);
            Assert.False(store.IsCurrent("8.0.0"));

            await IndexStore.WriteAsync(
                storePath, CreateManifest(version: "8.0.0"), [CreateComponent("MudButton")], [], TestContext.Current.CancellationToken);

            Assert.True(store.IsCurrent("8.0.0"));
            Assert.False(store.IsCurrent("9.0.0"));
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }

    [Fact]
    public async Task TryReadComponent_IsCaseInsensitiveAndAddsMudPrefix()
    {
        var storePath = NewStorePath();
        try
        {
            await IndexStore.WriteAsync(
                storePath, CreateManifest(), [CreateComponent("MudButton")], [], TestContext.Current.CancellationToken);

            var store = new IndexStore(storePath);

            Assert.NotNull(store.TryReadComponent("mudbutton"));
            Assert.NotNull(store.TryReadComponent("MUDBUTTON"));
            Assert.NotNull(store.TryReadComponent("Button"));
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }

    [Fact]
    public async Task TryReadComponent_StripsGenericSuffix()
    {
        var storePath = NewStorePath();
        try
        {
            await IndexStore.WriteAsync(
                storePath, CreateManifest(), [CreateComponent("MudButton")], [], TestContext.Current.CancellationToken);

            var component = new IndexStore(storePath).TryReadComponent("MudButton<T>");

            Assert.NotNull(component);
            Assert.Equal("MudButton", component.Name);
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }

    [Theory]
    [InlineData("../x")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("")]
    [InlineData(OverLongMarker)]
    [InlineData("Mud Button")]
    public void NormalizeName_RejectsUnsafeInput(string input)
    {
        var value = input == OverLongMarker ? new string('a', 129) : input;
        Assert.Null(IndexStore.NormalizeName(value));
    }

    [Fact]
    public async Task TryReadEnum_IsCaseInsensitive()
    {
        var storePath = NewStorePath();
        try
        {
            await IndexStore.WriteAsync(
                storePath, CreateManifest(), [CreateComponent("MudButton")], [CreateEnum("Color")], TestContext.Current.CancellationToken);

            var store = new IndexStore(storePath);

            Assert.NotNull(store.TryReadEnum("color"));
            Assert.NotNull(store.TryReadEnum("COLOR"));
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }

    [Theory]
    [InlineData("catalog.json", false)]
    [InlineData("catalog.json", true)]
    [InlineData("enums.json", false)]
    [InlineData("enums.json", true)]
    [InlineData("components/mudbutton.json", true)]
    public async Task RequiredPayload_MissingOrNull_Throws(string file, bool nullPayload)
    {
        var storePath = NewStorePath();
        try
        {
            await IndexStore.WriteAsync(
                storePath, CreateManifest(), [CreateComponent("MudButton")], [CreateEnum("Color")],
                TestContext.Current.CancellationToken);
            var path = Path.Combine(storePath, file.Replace('/', Path.DirectorySeparatorChar));
            if (nullPayload)
                await File.WriteAllTextAsync(path, "null", TestContext.Current.CancellationToken);
            else
                File.Delete(path);

            var store = new IndexStore(storePath);
            Assert.True(store.IsCurrent("8.0.0"));
            Action read = file switch
            {
                "catalog.json" => () => store.ReadCatalog(),
                "enums.json" => () => store.ReadEnums(),
                _ => () => store.TryReadComponent("MudButton"),
            };
            if (nullPayload)
            {
                var exception = Assert.Throws<JsonException>(read);
                Assert.Contains(path, exception.Message);
            }
            else
            {
                var exception = Assert.Throws<FileNotFoundException>(read);
                Assert.Equal(path, exception.FileName);
            }
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }

    [Fact]
    public async Task EmptyListsAndAbsentComponent_RemainValid()
    {
        var storePath = NewStorePath();
        try
        {
            await IndexStore.WriteAsync(
                storePath, CreateManifest(componentCount: 0, enumCount: 0), [], [],
                TestContext.Current.CancellationToken);
            var store = new IndexStore(storePath);

            Assert.Empty(store.ReadCatalog());
            Assert.Empty(store.ReadEnums());
            Assert.Null(store.TryReadComponent("Button"));
            Assert.Null(store.TryReadComponent("MudButton"));
            Assert.Null(store.TryReadEnum("Color"));
        }
        finally
        {
            TryDeleteParent(storePath);
        }
    }
}
