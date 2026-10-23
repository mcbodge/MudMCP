// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MudDocs.Models;

namespace MudDocs.Services;

/// <summary>
/// Metadata describing a persisted index store, written alongside the component and enum data.
/// </summary>
internal sealed record StoreManifest(
    int SchemaVersion,
    string MudBlazorVersion,
    DateTimeOffset BuiltAtUtc,
    int ComponentCount,
    int EnumCount,
    bool IncludeInternalComponents,
    bool IncludeDeprecatedComponents,
    int MaxExamplesPerComponent);

/// <summary>
/// Source-generated serialization metadata for the index store, using camelCase property names
/// and omitting default values to keep the on-disk payload compact.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault)]
[JsonSerializable(typeof(StoreManifest))]
[JsonSerializable(typeof(ComponentInfo))]
[JsonSerializable(typeof(List<ComponentInfo>))]
[JsonSerializable(typeof(List<EnumInfo>))]
internal sealed partial class IndexStoreJsonContext : JsonSerializerContext;

/// <summary>
/// Reads and writes a versioned, on-disk index store (manifest, catalog, per-component files and enums).
/// </summary>
internal sealed class IndexStore(string storePath)
{
    internal const int SchemaVersion = 1;

    /// <summary>
    /// Writes a fresh store atomically: everything is staged in a sibling temp folder, then swapped
    /// into place so a reader never sees a partially written store.
    /// </summary>
    internal static async Task WriteAsync(
        string storePath,
        StoreManifest manifest,
        IReadOnlyCollection<ComponentInfo> components,
        IReadOnlyCollection<EnumInfo> enums,
        CancellationToken cancellationToken)
    {
        var parent = Path.GetDirectoryName(storePath) ?? string.Empty;
        var tempPath = Path.Combine(parent, "store.tmp-" + Guid.NewGuid().ToString("N"));
        string? oldPath = null;

        try
        {
            // 1. Stage the new store under a temp folder with a components subfolder.
            var componentsDir = Path.Combine(tempPath, "components");
            Directory.CreateDirectory(componentsDir);

            // 2. One file per component, keyed by lower-cased name.
            foreach (var component in components)
            {
                var componentPath = Path.Combine(componentsDir, $"{component.Name.ToLowerInvariant()}.json");
                await WriteJsonAsync(componentPath, component, IndexStoreJsonContext.Default.ComponentInfo, cancellationToken);
            }

            // 3. Catalog: components sorted by name, with example source code stripped out.
            var catalog = components
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(StripExampleCode)
                .ToList();
            await WriteJsonAsync(Path.Combine(tempPath, "catalog.json"), catalog, IndexStoreJsonContext.Default.ListComponentInfo, cancellationToken);

            // 4. Enums sorted by name.
            var sortedEnums = enums
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            await WriteJsonAsync(Path.Combine(tempPath, "enums.json"), sortedEnums, IndexStoreJsonContext.Default.ListEnumInfo, cancellationToken);

            // 5. Manifest written last so its presence signals a complete store.
            await WriteJsonAsync(Path.Combine(tempPath, "manifest.json"), manifest, IndexStoreJsonContext.Default.StoreManifest, cancellationToken);

            // 6. Move any existing store aside.
            if (Directory.Exists(storePath))
            {
                oldPath = Path.Combine(parent, "store.old-" + Guid.NewGuid().ToString("N"));
                Directory.Move(storePath, oldPath);
            }

            // 7. Promote the staged store into place.
            Directory.Move(tempPath, storePath);

            // 8. Drop the superseded store.
            if (oldPath is not null)
            {
                Directory.Delete(oldPath, recursive: true);
            }
        }
        catch (Exception)
        {
            try
            {
                if (!Directory.Exists(storePath) && oldPath is not null)
                {
                    Directory.Move(oldPath, storePath);
                }

                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Cleanup is best-effort; the original exception is rethrown below.
            }

            throw;
        }
    }

    /// <summary>
    /// Reads the manifest, or returns <see langword="null"/> when the store has no manifest.
    /// </summary>
    internal StoreManifest? TryReadManifest()
    {
        var path = Path.Combine(storePath, "manifest.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(path);
        return JsonSerializer.Deserialize(bytes, IndexStoreJsonContext.Default.StoreManifest);
    }

    /// <summary>
    /// Returns <see langword="true"/> only when a manifest exists with the current schema and the given version.
    /// </summary>
    internal bool IsCurrent(string version)
    {
        var manifest = TryReadManifest();
        return manifest is not null
            && manifest.SchemaVersion == SchemaVersion
            && string.Equals(manifest.MudBlazorVersion, version, StringComparison.Ordinal);
    }

    /// <summary>
    /// Normalizes a caller-supplied component or type name into a safe file-name token, or returns
    /// <see langword="null"/> when the name is unsafe.
    /// </summary>
    internal static string? NormalizeName(string name)
    {
        var normalized = name.Trim();

        var angle = normalized.IndexOf('<');
        if (angle >= 0)
        {
            normalized = normalized[..angle];
        }

        if (normalized.Length is < 1 or > 128)
        {
            return null;
        }

        foreach (var c in normalized)
        {
            var isSafe = c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_';
            if (!isSafe)
            {
                return null;
            }
        }

        return normalized;
    }

    /// <summary>
    /// Reads a single component by name (case-insensitive, with a <c>Mud</c>-prefix fallback), or
    /// returns <see langword="null"/> when the name is invalid or no matching file exists.
    /// </summary>
    internal ComponentInfo? TryReadComponent(string name)
    {
        var normalized = NormalizeName(name);
        if (normalized is null)
        {
            return null;
        }

        var componentsDir = Path.Combine(storePath, "components");
        var lower = normalized.ToLowerInvariant();
        var path = Path.Combine(componentsDir, $"{lower}.json");

        if (!File.Exists(path))
        {
            if (normalized.StartsWith("Mud", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            path = Path.Combine(componentsDir, $"mud{lower}.json");
            if (!File.Exists(path))
            {
                return null;
            }
        }

        var bytes = File.ReadAllBytes(path);
        return JsonSerializer.Deserialize(bytes, IndexStoreJsonContext.Default.ComponentInfo)
            ?? throw new JsonException($"Component payload in '{path}' must not be null.");
    }

    /// <summary>
    /// Reads the required component catalog.
    /// </summary>
    internal IReadOnlyList<ComponentInfo> ReadCatalog()
    {
        var path = Path.Combine(storePath, "catalog.json");
        var bytes = File.ReadAllBytes(path);
        return JsonSerializer.Deserialize(bytes, IndexStoreJsonContext.Default.ListComponentInfo)
            ?? throw new JsonException($"Component catalog payload in '{path}' must not be null.");
    }

    /// <summary>
    /// Reads the required enum list.
    /// </summary>
    internal IReadOnlyList<EnumInfo> ReadEnums()
    {
        var path = Path.Combine(storePath, "enums.json");
        var bytes = File.ReadAllBytes(path);
        return JsonSerializer.Deserialize(bytes, IndexStoreJsonContext.Default.ListEnumInfo)
            ?? throw new JsonException($"Enum list payload in '{path}' must not be null.");
    }

    /// <summary>
    /// Reads a single enum by name (case-insensitive exact match), or <see langword="null"/>.
    /// </summary>
    internal EnumInfo? TryReadEnum(string name) =>
        ReadEnums().FirstOrDefault(e => string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase));

    private static ComponentInfo StripExampleCode(ComponentInfo component) =>
        component with
        {
            Examples = component.Examples
                .Select(e => e with { RazorMarkup = null, CSharpCode = null })
                .ToList(),
        };

    private static async Task WriteJsonAsync<T>(string path, T value, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, typeInfo);
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
    }
}
