// Copyright (c) 2025 Mud MCP Contributors
// Licensed under the GNU General Public License v2.0. See LICENSE file in the project root for full license information.

namespace MudBlazor.Mcp.Services;

/// <summary>
/// Builds the component index in the background so the MCP transport can answer the
/// <c>initialize</c> handshake immediately (avoiding client startup timeouts on the first,
/// slower cold-start clone). Tools report the index as "not ready" until the build completes.
/// </summary>
public sealed class IndexInitializationService : BackgroundService
{
    private readonly IComponentIndexer _indexer;
    private readonly ILogger<IndexInitializationService> _logger;

    public IndexInitializationService(IComponentIndexer indexer, ILogger<IndexInitializationService> logger)
    {
        _indexer = indexer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield so host startup completes before the (potentially long) first-run clone/build.
        await Task.Yield();

        // Bounded retry so a transient failure (e.g. a flaky clone) doesn't leave the index
        // permanently unbuilt for the process lifetime.
        var delays = new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30) };

        for (var attempt = 0; ; attempt++)
        {
            try
            {
                _logger.LogInformation("Building MudBlazor component index in the background (attempt {Attempt})...", attempt + 1);
                await _indexer.BuildIndexAsync(stoppingToken);
                var count = (await _indexer.GetAllComponentsAsync(stoppingToken)).Count;
                _logger.LogInformation("Index built successfully with {ComponentCount} components", count);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Index build cancelled during shutdown.");
                return;
            }
            catch (Exception ex)
            {
                if (attempt >= delays.Length)
                {
                    _logger.LogError(ex,
                        "Failed to build the component index after {Attempts} attempts. Tools will report the index as " +
                        "not ready; restart the server to retry.", attempt + 1);
                    return;
                }

                var delay = delays[attempt];
                _logger.LogWarning(ex, "Index build attempt {Attempt} failed; retrying in {Delay}s.", attempt + 1, delay.TotalSeconds);
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
            }
        }
    }
}
