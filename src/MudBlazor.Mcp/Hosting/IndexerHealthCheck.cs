// Copyright (c) 2026 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

using Microsoft.Extensions.Diagnostics.HealthChecks;
using MudBlazor.Mcp.Services;

namespace MudBlazor.Mcp.Hosting;

/// <summary>Health check for the component indexer with detailed status.</summary>
public class IndexerHealthCheck : IHealthCheck
{
    private readonly IComponentIndexer _indexer;
    private readonly ILogger<IndexerHealthCheck> _logger;

    public IndexerHealthCheck(IComponentIndexer indexer, ILogger<IndexerHealthCheck> logger)
    {
        _indexer = indexer;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_indexer.IsIndexed)
            {
                _logger.LogDebug("Indexer health check: Index not yet built");
                return HealthCheckResult.Degraded(
                    "Component index is not yet built. The server may still be initializing.",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "building",
                        ["componentCount"] = 0,
                        ["isIndexed"] = false
                    });
            }

            var components = await _indexer.GetAllComponentsAsync(cancellationToken);
            var categories = await _indexer.GetCategoriesAsync(cancellationToken);

            if (components.Count == 0)
            {
                _logger.LogWarning("Indexer health check: No components indexed");
                return HealthCheckResult.Degraded(
                    "Component index is empty. There may be a parsing issue.",
                    data: new Dictionary<string, object>
                    {
                        ["status"] = "empty",
                        ["componentCount"] = 0,
                        ["categoryCount"] = categories.Count,
                        ["isIndexed"] = true,
                        ["lastIndexed"] = _indexer.LastIndexed?.ToString("O") ?? "never"
                    });
            }

            _logger.LogDebug("Indexer health check passed: {Count} components", components.Count);
            return HealthCheckResult.Healthy(
                $"Index contains {components.Count} components in {categories.Count} categories.",
                data: new Dictionary<string, object>
                {
                    ["status"] = "ready",
                    ["componentCount"] = components.Count,
                    ["categoryCount"] = categories.Count,
                    ["isIndexed"] = true,
                    ["lastIndexed"] = _indexer.LastIndexed?.ToString("O") ?? "never"
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexer health check failed");
            return HealthCheckResult.Unhealthy(
                "Failed to check component index health.",
                ex,
                data: new Dictionary<string, object>
                {
                    ["status"] = "error",
                    ["error"] = ex.Message
                });
        }
    }
}
