// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;
using MudDocs.Cli;
using MudDocs.Configuration;
using MudDocs.Services;

namespace MudDocs.Tests.Cli;

public class CliIndexCommandTests
{
    private static Func<string, string?> EmptyEnv() => _ => null;

    private static string NewDataPath() =>
        Path.Combine(Path.GetTempPath(), "muddocs-idx-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Writes a valid <c>index.json</c> for <paramref name="version"/> so that
    /// <c>BuildIndexAsync</c> loads from the cache and never clones the repository.
    /// Serialized with the default <see cref="JsonSerializer"/> (PascalCase) to match the
    /// cache reader.
    /// </summary>
    private static void WriteIndexJson(string dataPath, string version)
    {
        var versionDir = Path.Combine(dataPath, "v" + version);
        Directory.CreateDirectory(versionDir);

        var payload = new
        {
            SchemaVersion = ComponentIndexer.CacheSchemaVersion,
            IncludeInternalComponents = false,
            IncludeDeprecatedComponents = true,
            MaxExamplesPerComponent = 20,
            Components = new[]
            {
                new
                {
                    Name = "MudButton",
                    Namespace = "MudBlazor",
                    Summary = "A button",
                    Description = (string?)null,
                    Category = "Buttons",
                    BaseType = (string?)null,
                    Parameters = Array.Empty<object>(),
                    Events = Array.Empty<object>(),
                    Methods = Array.Empty<object>(),
                    Examples = Array.Empty<object>(),
                    RelatedComponents = Array.Empty<string>(),
                    DocumentationUrl = (string?)null,
                    SourceUrl = (string?)null,
                },
            },
            ApiReferences = Array.Empty<object>(),
            Enums = Array.Empty<object>(),
        };

        File.WriteAllText(Path.Combine(versionDir, "index.json"), JsonSerializer.Serialize(payload));
    }

    private static void TryDelete(string dataPath)
    {
        try
        {
            if (Directory.Exists(dataPath))
            {
                Directory.Delete(dataPath, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            // Best-effort cleanup of the temp directory.
        }
    }

    [Fact]
    public async Task Index_WithValidIndexJson_DerivesStoreWithoutCloning()
    {
        var dataPath = NewDataPath();
        try
        {
            WriteIndexJson(dataPath, "9.0.0");

            var stdout = new StringWriter();
            var stderr = new StringWriter();

            var exit = await CliApp.RunAsync(
                ["index", "--version", "9.0.0", "--data-path", dataPath],
                stdout, stderr, EmptyEnv(), TestContext.Current.CancellationToken);

            Assert.Equal(0, exit);
            Assert.StartsWith("9.0.0 ready components=1 enums=0", stdout.ToString());
            Assert.True(new IndexStore(new VersionContext("9.0.0", dataPath).StorePath).IsCurrent("9.0.0"));
        }
        finally
        {
            TryDelete(dataPath);
        }
    }

    [Fact]
    public async Task Index_WithoutVersion_ReturnsUsageErrorWithHint()
    {
        var ct = TestContext.Current.CancellationToken;

        // (a) A ready store is present: the hint lists the indexed versions.
        var dataPath = NewDataPath();
        try
        {
            WriteIndexJson(dataPath, "9.0.0");

            var buildOut = new StringWriter();
            var buildErr = new StringWriter();
            var buildExit = await CliApp.RunAsync(
                ["index", "--version", "9.0.0", "--data-path", dataPath],
                buildOut, buildErr, EmptyEnv(), ct);
            Assert.Equal(0, buildExit);

            var stdout2 = new StringWriter();
            var stderr2 = new StringWriter();
            var exit2 = await CliApp.RunAsync(
                ["index", "--data-path", dataPath],
                stdout2, stderr2, EmptyEnv(), ct);

            Assert.Equal(2, exit2);
            Assert.Contains("hint: indexed versions: 9.0.0", stderr2.ToString());
        }
        finally
        {
            TryDelete(dataPath);
        }

        // (b) An empty data path (no versions): the hint points at the project files.
        var emptyDataPath = NewDataPath();
        try
        {
            Directory.CreateDirectory(emptyDataPath);

            var stdout3 = new StringWriter();
            var stderr3 = new StringWriter();
            var exit3 = await CliApp.RunAsync(
                ["index", "--data-path", emptyDataPath],
                stdout3, stderr3, EmptyEnv(), ct);

            Assert.Equal(2, exit3);
            Assert.Contains("read Version from <PackageReference", stderr3.ToString());
        }
        finally
        {
            TryDelete(emptyDataPath);
        }
    }
}
