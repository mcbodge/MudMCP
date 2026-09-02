// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using MudBlazor.Mcp.Configuration;
using MudBlazor.Mcp.Models;
using MudBlazor.Mcp.Services.Parsing;

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Indexes and queries MudBlazor component documentation.
/// </summary>
public sealed class ComponentIndexer : IComponentIndexer
{
    private readonly IGitRepositoryService _gitService;
    private readonly SemanticComponentParser _semanticParser;
    private readonly XmlDocParser _xmlParser;
    private readonly RazorDocParser _razorParser;
    private readonly ExampleExtractor _exampleExtractor;
    private readonly MenuCategoryParser _menuCategoryParser;
    private readonly ILogger<ComponentIndexer> _logger;
    private readonly MudBlazorOptions _options;
    private readonly VersionContext _versionContext;

    private const string RazorCsExtension = ".razor.cs";
    private const string CsExtension = ".cs";

    private readonly ConcurrentDictionary<string, ComponentInfo> _components = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, ApiReference> _apiReferences = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, EnumInfo> _enums = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    
    private bool _isIndexed;
    private DateTimeOffset? _lastIndexed;

    public bool IsIndexed => _isIndexed;
    public DateTimeOffset? LastIndexed => _lastIndexed;

    public ComponentIndexer(
        IGitRepositoryService gitService,
        SemanticComponentParser semanticParser,
        XmlDocParser xmlParser,
        RazorDocParser razorParser,
        ExampleExtractor exampleExtractor,
        MenuCategoryParser menuCategoryParser,
        VersionContext versionContext,
        IOptions<MudBlazorOptions> options,
        ILogger<ComponentIndexer> logger)
    {
        _gitService = gitService;
        _semanticParser = semanticParser;
        _xmlParser = xmlParser;
        _razorParser = razorParser;
        _exampleExtractor = exampleExtractor;
        _menuCategoryParser = menuCategoryParser;
        _versionContext = versionContext;
        _logger = logger;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task BuildIndexAsync(CancellationToken cancellationToken = default)
    {
        await _indexLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Try loading from serialized cache first
            if (await TryLoadCachedIndexAsync(cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Loaded cached index for v{Version} with {Count} components",
                    _versionContext.Version, _components.Count);

                // Categories are baked into each cached ComponentInfo and reconstructed on demand,
                // so no repository clone or menu parse is required on the fast cached-load path.
                _isIndexed = true;
                _lastIndexed = DateTimeOffset.UtcNow;
                return;
            }

            _logger.LogInformation("Starting index build for v{Version}...", _versionContext.Version);
            var sw = System.Diagnostics.Stopwatch.StartNew();

            // Clear any stale in-memory state before performing a full rebuild so that
            // removed components/API refs from a previous run don't leak into the new index.
            _components.Clear();
            _apiReferences.Clear();
            _enums.Clear();

            await _gitService.EnsureRepositoryAsync(cancellationToken).ConfigureAwait(false);

            if (!_gitService.IsAvailable)
            {
                throw new InvalidOperationException("Repository is not available for indexing");
            }

            var repoPath = _gitService.RepositoryPath!;

            await _menuCategoryParser.InitializeAsync(repoPath, cancellationToken).ConfigureAwait(false);
            var compiledSource = await _semanticParser.CompileAsync(repoPath, cancellationToken).ConfigureAwait(false);

            await IndexComponentsAsync(repoPath, compiledSource, cancellationToken).ConfigureAwait(false);
            IndexEnums(compiledSource);
            await IndexDocumentationAsync(repoPath, cancellationToken).ConfigureAwait(false);
            await IndexExamplesAsync(repoPath, cancellationToken).ConfigureAwait(false);

            _isIndexed = true;
            _lastIndexed = DateTimeOffset.UtcNow;

            // Serialize index to disk for future fast loads
            await SaveCachedIndexAsync(cancellationToken).ConfigureAwait(false);

            sw.Stop();
            _logger.LogInformation("Index build completed in {ElapsedMs}ms. Indexed {Count} components, {EnumCount} enums",
                sw.ElapsedMilliseconds, _components.Count, _enums.Count);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    /// Schema version for the on-disk cache format. Increment this value whenever the
    /// <see cref="CachedIndex"/> record structure changes or the serialization format
    /// is updated in a backward-incompatible way, so stale caches are automatically rebuilt.
    /// </summary>
    private const int CacheSchemaVersion = 6;

    private async Task<bool> TryLoadCachedIndexAsync(CancellationToken cancellationToken)
    {
        var indexPath = _versionContext.IndexPath;
        if (!File.Exists(indexPath)) return false;

        try
        {
            var json = await File.ReadAllTextAsync(indexPath, cancellationToken).ConfigureAwait(false);
            var cached = JsonSerializer.Deserialize<CachedIndex>(json);
            if (cached is null) return false;

            // Invalidate if schema version or parsing options don't match the current configuration.
            if (cached.SchemaVersion != CacheSchemaVersion
                || cached.IncludeInternalComponents != _options.Parsing.IncludeInternalComponents
                || cached.IncludeDeprecatedComponents != _options.Parsing.IncludeDeprecatedComponents
                || cached.MaxExamplesPerComponent != _options.Parsing.MaxExamplesPerComponent)
            {
                _logger.LogInformation("Cached index at {Path} is stale (schema or options mismatch), will rebuild", indexPath);
                TryDeleteCacheFile(indexPath);
                return false;
            }

            _components.Clear();
            foreach (var component in cached.Components)
                _components[component.Name] = component;

            _apiReferences.Clear();
            foreach (var apiRef in cached.ApiReferences)
                _apiReferences[apiRef.TypeName] = apiRef;

            _enums.Clear();
            foreach (var enumInfo in cached.Enums)
                _enums[enumInfo.Name] = enumInfo;

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cached index from {Path}, will rebuild", indexPath);
            TryDeleteCacheFile(indexPath);
            return false;
        }
    }

    private async Task SaveCachedIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cached = new CachedIndex(
                CacheSchemaVersion,
                _options.Parsing.IncludeInternalComponents,
                _options.Parsing.IncludeDeprecatedComponents,
                _options.Parsing.MaxExamplesPerComponent,
                _components.Values.ToList(),
                _apiReferences.Values.ToList(),
                _enums.Values.ToList());

            var dir = Path.GetDirectoryName(_versionContext.IndexPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(
                cached,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                });
            await File.WriteAllTextAsync(_versionContext.IndexPath, json, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Saved index cache to {Path}", _versionContext.IndexPath);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save index cache — server will work but next startup will re-index");
        }
    }

    private void TryDeleteCacheFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete stale cache file {Path}", path);
        }
    }

    private sealed record CachedIndex(
        int SchemaVersion,
        bool IncludeInternalComponents,
        bool IncludeDeprecatedComponents,
        int MaxExamplesPerComponent,
        List<ComponentInfo> Components,
        List<ApiReference> ApiReferences,
        List<EnumInfo> Enums);

    private async Task IndexComponentsAsync(string repoPath, CompiledSource source, CancellationToken cancellationToken)
    {
        var componentsPath = Path.Combine(repoPath, "src", "MudBlazor", "Components");
        
        if (!Directory.Exists(componentsPath))
        {
            _logger.LogWarning("Components directory not found: {Path}", componentsPath);
            return;
        }

        var componentDirs = Directory.GetDirectories(componentsPath);
        _logger.LogDebug("Found {Count} component directories", componentDirs.Length);

        var tasks = componentDirs.Select(dir => IndexComponentDirectoryAsync(dir, source, cancellationToken));
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task IndexComponentDirectoryAsync(string componentDir, CompiledSource source, CancellationToken cancellationToken)
    {
        var dirName = Path.GetFileName(componentDir);
        
        // Collect all Mud*.razor.cs files
        var razorCsFiles = Directory.GetFiles(componentDir, $"Mud*{RazorCsExtension}");
        
        // Collect Mud*.cs files that don't have a corresponding .razor.cs file
        var razorCsSet = new HashSet<string>(
            razorCsFiles.Select(f => f[..^RazorCsExtension.Length]),
            StringComparer.OrdinalIgnoreCase);
        
        var csOnlyFiles = Directory.GetFiles(componentDir, $"Mud*{CsExtension}")
            .Where(f => !f.EndsWith(RazorCsExtension, StringComparison.OrdinalIgnoreCase)
                        && !razorCsSet.Contains(f[..^CsExtension.Length]));

        var allFiles = razorCsFiles.Concat(csOnlyFiles);

        foreach (var file in allFiles)
        {
            await IndexSingleComponentFileAsync(file, dirName, source, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task IndexSingleComponentFileAsync(string filePath, string dirName, CompiledSource source, CancellationToken cancellationToken)
    {
        try
        {
            var parseResult = await ExtractComponentAsync(filePath, source, cancellationToken).ConfigureAwait(false);

            if (parseResult is null)
            {
                return;
            }

            var componentName = parseResult.ClassName;

            // Categories are derived from the version's documentation menu; a null result means the
            // component is not listed in the menu (per version-accurate parsing decision D4).
            var category = _menuCategoryParser.GetCategoryName(componentName);

            var componentInfo = new ComponentInfo(
                Name: componentName,
                Namespace: parseResult.Namespace ?? "MudBlazor",
                Summary: parseResult.Summary ?? $"{componentName} component",
                Description: parseResult.Remarks,
                Category: category,
                BaseType: parseResult.BaseType,
                Parameters: parseResult.Parameters,
                Events: parseResult.Events,
                Methods: parseResult.Methods,
                Examples: [],
                RelatedComponents: [],
                DocumentationUrl: $"https://mudblazor.com/components/{dirName.ToLowerInvariant()}",
                SourceUrl: $"{TrimTrailingGitSuffix(_options.Repository.Url)}/tree/{_versionContext.Tag}/src/MudBlazor/Components/{dirName}"
            );

            _components[componentName] = componentInfo;
            _logger.LogTrace("Indexed component: {Name}", componentName);

            // Also index as API reference
            _apiReferences[componentName] = CreateApiReference(parseResult);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "IO error indexing component in: {Dir}", dirName);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied indexing component in: {Dir}", dirName);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to index component in: {Dir}", dirName);
        }
    }

    /// <summary>
    /// Extracts component information using the semantic compilation (full base-chain merge and
    /// <c>&lt;inheritdoc/&gt;</c> resolution). If the component's type symbol cannot be resolved or the
    /// semantic extraction fails, falls back to the syntax-only <see cref="XmlDocParser"/> which yields
    /// a degraded, own-declared-members-only result.
    /// </summary>
    private async Task<ComponentParseResult?> ExtractComponentAsync(string filePath, CompiledSource source, CancellationToken cancellationToken)
    {
        var componentName = GetComponentNameFromFile(filePath);

        if (source.TypesByName.TryGetValue(componentName, out var typeSymbol))
        {
            try
            {
                return _semanticParser.ExtractComponent(source, typeSymbol);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex,
                    "Semantic extraction failed for {Component}; falling back to XmlDocParser", componentName);
            }
        }

        return await _xmlParser.ParseComponentFileAsync(filePath, cancellationToken).ConfigureAwait(false);
    }

    private static string GetComponentNameFromFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);

        if (fileName.EndsWith(RazorCsExtension, StringComparison.OrdinalIgnoreCase))
            return fileName[..^RazorCsExtension.Length];

        if (fileName.EndsWith(CsExtension, StringComparison.OrdinalIgnoreCase))
            return fileName[..^CsExtension.Length];

        return fileName;
    }

    private void IndexEnums(CompiledSource source)
    {
        foreach (var enumInfo in _semanticParser.ExtractEnums(source))
        {
            _enums[enumInfo.Name] = enumInfo;
        }

        _logger.LogDebug("Indexed {Count} enums", _enums.Count);
    }

    private static ApiReference CreateApiReference(ComponentParseResult parseResult)
    {
        var members = new List<ApiMember>();

        // Add parameters as properties
        foreach (var param in parseResult.Parameters)
        {
            members.Add(new ApiMember(
                Name: param.Name,
                MemberType: "Property",
                ReturnType: param.Type,
                Description: param.Description
            ));
        }

        // Add events
        foreach (var evt in parseResult.Events)
        {
            members.Add(new ApiMember(
                Name: evt.Name,
                MemberType: "Event",
                ReturnType: evt.EventArgsType is not null 
                    ? $"EventCallback<{evt.EventArgsType}>" 
                    : "EventCallback",
                Description: evt.Description
            ));
        }

        // Add methods
        foreach (var method in parseResult.Methods)
        {
            var paramSignature = method.Parameters.Count > 0
                ? string.Join(", ", method.Parameters.Select(p => $"{p.Type} {p.Name}"))
                : null;

            members.Add(new ApiMember(
                Name: method.Name,
                MemberType: "Method",
                ReturnType: method.ReturnType,
                Description: method.Description,
                ParameterSignature: paramSignature
            ));
        }

        return new ApiReference(
            TypeName: parseResult.ClassName,
            Namespace: parseResult.Namespace ?? "MudBlazor",
            Summary: parseResult.Summary,
            BaseType: parseResult.BaseType,
            Members: members
        );
    }

    private async Task IndexDocumentationAsync(string repoPath, CancellationToken cancellationToken)
    {
        var docsPath = Path.Combine(repoPath, "src", "MudBlazor.Docs", "Pages", "Components");
        
        if (!Directory.Exists(docsPath))
        {
            _logger.LogWarning("Documentation directory not found: {Path}", docsPath);
            return;
        }

        var docFiles = Directory.GetFiles(docsPath, "*Page.razor", SearchOption.AllDirectories);
        _logger.LogDebug("Found {Count} documentation files", docFiles.Length);

        foreach (var docFile in docFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var docResult = await _razorParser.ParseDocumentationFileAsync(docFile, cancellationToken).ConfigureAwait(false);
                
                if (docResult?.ComponentName is not null && _components.TryGetValue(docResult.ComponentName, out var component))
                {
                    // Enhance component with documentation info
                    var enhanced = component with
                    {
                        Description = docResult.Description ?? component.Description,
                        RelatedComponents = docResult.RelatedComponents
                    };
                    
                    _components[docResult.ComponentName] = enhanced;
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "IO error parsing documentation file: {File}", docFile);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied parsing documentation file: {File}", docFile);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to parse documentation file: {File}", docFile);
            }
        }
    }

    private async Task IndexExamplesAsync(string repoPath, CancellationToken cancellationToken)
    {
        var docsPath = Path.Combine(repoPath, "src", "MudBlazor.Docs");
        
        foreach (var (componentName, component) in _components)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var examples = await _exampleExtractor.ExtractExamplesAsync(docsPath, componentName, cancellationToken).ConfigureAwait(false);
                
                if (examples.Count > 0)
                {
                    var enhanced = component with { Examples = examples };
                    _components[componentName] = enhanced;
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "IO error extracting examples for: {Component}", componentName);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied extracting examples for: {Component}", componentName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Failed to extract examples for: {Component}", componentName);
            }
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ComponentInfo>> GetAllComponentsAsync(CancellationToken cancellationToken = default)
    {
        EnsureIndexed();
        return Task.FromResult<IReadOnlyList<ComponentInfo>>(_components.Values.ToList());
    }

    /// <inheritdoc />
    public Task<ComponentInfo?> GetComponentAsync(string componentName, CancellationToken cancellationToken = default)
    {
        EnsureIndexed();
        
        // Try exact match first
        if (_components.TryGetValue(componentName, out var component))
        {
            return Task.FromResult<ComponentInfo?>(component);
        }

        // Try with "Mud" prefix
        if (!componentName.StartsWith("Mud", StringComparison.OrdinalIgnoreCase))
        {
            if (_components.TryGetValue($"Mud{componentName}", out component))
            {
                return Task.FromResult<ComponentInfo?>(component);
            }
        }

        return Task.FromResult<ComponentInfo?>(null);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ComponentCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        EnsureIndexed();

        // Reconstruct categories from the indexed components (categories are menu-derived and stored
        // on each ComponentInfo), so this works identically on both fresh and cached-load paths.
        var categories = _components.Values
            .Where(c => !string.IsNullOrEmpty(c.Category))
            .GroupBy(c => c.Category!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ComponentCategory(
                Name: g.Key,
                Title: g.Key,
                Description: null,
                ComponentNames: g.Select(c => c.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Task.FromResult<IReadOnlyList<ComponentCategory>>(categories);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ComponentInfo>> GetComponentsByCategoryAsync(string category, CancellationToken cancellationToken = default)
    {
        EnsureIndexed();
        
        var components = _components.Values
            .Where(c => c.Category?.Equals(category, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        return Task.FromResult<IReadOnlyList<ComponentInfo>>(components);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ComponentInfo>> SearchComponentsAsync(
        string query,
        SearchFields searchFields = SearchFields.All,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        EnsureIndexed();

        var queryLower = query.ToLowerInvariant();
        var results = new List<(ComponentInfo Component, int Score)>();

        foreach (var component in _components.Values)
        {
            var score = CalculateSearchScore(component, queryLower, searchFields);
            if (score > 0)
            {
                results.Add((component, score));
            }
        }

        var sorted = results
            .OrderByDescending(r => r.Score)
            .Take(maxResults)
            .Select(r => r.Component)
            .ToList();

        return Task.FromResult<IReadOnlyList<ComponentInfo>>(sorted);
    }

    private static int CalculateSearchScore(ComponentInfo component, string query, SearchFields fields)
    {
        var score = 0;

        if (fields.HasFlag(SearchFields.Name))
        {
            if (component.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                score += component.Name.Equals(query, StringComparison.OrdinalIgnoreCase) ? 100 : 50;
            }
        }

        if (fields.HasFlag(SearchFields.Description))
        {
            if (component.Summary?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                score += 30;
            if (component.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                score += 20;
        }

        if (fields.HasFlag(SearchFields.Parameters))
        {
            foreach (var param in component.Parameters)
            {
                if (param.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    score += 10;
                if (param.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                    score += 5;
            }
        }

        if (fields.HasFlag(SearchFields.Examples))
        {
            foreach (var example in component.Examples)
            {
                if (example.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                    score += 5;
            }
        }

        return score;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ComponentExample>> GetExamplesAsync(string componentName, CancellationToken cancellationToken = default)
    {
        EnsureIndexed();

        if (_components.TryGetValue(componentName, out var component))
        {
            return Task.FromResult<IReadOnlyList<ComponentExample>>(component.Examples);
        }

        return Task.FromResult<IReadOnlyList<ComponentExample>>([]);
    }

    /// <inheritdoc />
    public Task<ApiReference?> GetApiReferenceAsync(string typeName, CancellationToken cancellationToken = default)
    {
        EnsureIndexed();

        if (_apiReferences.TryGetValue(typeName, out var apiRef))
        {
            return Task.FromResult<ApiReference?>(apiRef);
        }

        // Try with Mud prefix
        if (!typeName.StartsWith("Mud", StringComparison.OrdinalIgnoreCase))
        {
            if (_apiReferences.TryGetValue($"Mud{typeName}", out apiRef))
            {
                return Task.FromResult<ApiReference?>(apiRef);
            }
        }

        return Task.FromResult<ApiReference?>(null);
    }

    /// <inheritdoc />
    public Task<EnumInfo?> GetEnumAsync(string enumName, CancellationToken cancellationToken = default)
    {
        EnsureIndexed();

        return Task.FromResult(_enums.GetValueOrDefault(enumName));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<EnumInfo>> GetAllEnumsAsync(CancellationToken cancellationToken = default)
    {
        EnsureIndexed();

        return Task.FromResult<IReadOnlyList<EnumInfo>>(_enums.Values.ToList());
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ComponentInfo>> GetRelatedComponentsAsync(
        string componentName,
        RelationshipType relationshipType = RelationshipType.All,
        CancellationToken cancellationToken = default)
    {
        EnsureIndexed();

        if (!_components.TryGetValue(componentName, out var component))
        {
            return Task.FromResult<IReadOnlyList<ComponentInfo>>([]);
        }

        var related = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add explicitly related components
        foreach (var relatedName in component.RelatedComponents)
        {
            related.Add(relatedName);
        }

        // Add components in same category (siblings)
        if (relationshipType is RelationshipType.All or RelationshipType.Sibling)
        {
            if (component.Category is not null)
            {
                var siblings = _components.Values
                    .Where(c => c.Category?.Equals(component.Category, StringComparison.OrdinalIgnoreCase) == true
                                && !c.Name.Equals(componentName, StringComparison.OrdinalIgnoreCase));
                foreach (var sibling in siblings)
                {
                    related.Add(sibling.Name);
                }
            }
        }

        // Add parent (base type)
        if (relationshipType is RelationshipType.All or RelationshipType.Parent)
        {
            if (component.BaseType is not null && _components.ContainsKey(component.BaseType))
            {
                related.Add(component.BaseType);
            }
        }

        // Add children (components that inherit from this one)
        if (relationshipType is RelationshipType.All or RelationshipType.Child)
        {
            foreach (var (name, comp) in _components)
            {
                if (comp.BaseType?.Equals(componentName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    related.Add(name);
                }
            }
        }

        var relatedComponents = related
            .Where(r => _components.ContainsKey(r))
            .Select(r => _components[r])
            .Take(10)
            .ToList();

        return Task.FromResult<IReadOnlyList<ComponentInfo>>(relatedComponents);
    }

    private void EnsureIndexed()
    {
        if (!_isIndexed)
        {
            // Surfaced to MCP clients as a user-correctable "try again" while the background build runs,
            // so every indexer-backed tool reports startup readiness the same way.
            throw new McpException("Component index is not ready. The server may still be initializing. Please try again in a moment.");
        }
    }

    /// <summary>
    /// Removes a trailing ".git" suffix from a URL, leaving the rest of the URL intact.
    /// </summary>
    private static string TrimTrailingGitSuffix(string url)
    {
        var trimmed = url.TrimEnd('/');
        return trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }
}
