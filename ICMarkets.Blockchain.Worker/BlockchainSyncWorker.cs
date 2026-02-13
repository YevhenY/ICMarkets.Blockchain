using ICMarkets.Blockchain.Application.Features;
using MediatR;
using Microsoft.Extensions.Options;

namespace ICMarkets.Blockchain.Worker;

/// <summary>
/// Background service that periodically synchronizes blockchain data from BlockCypher API.
/// Uses MediatR to dispatch SyncBlockchainCommand at configured intervals.
/// </summary>
public class BlockchainSyncWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BlockchainSyncWorker> _logger;
    private readonly WorkerSettings _settings;

    public BlockchainSyncWorker(
        IServiceProvider serviceProvider,
        ILogger<BlockchainSyncWorker> logger,
        IOptions<WorkerSettings> settings)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "BlockchainSyncWorker started. Sync interval: {IntervalMinutes} minutes",
            _settings.SyncIntervalMinutes);

        // Wait before first execution to allow infrastructure to initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_settings.SyncIntervalMinutes));

        try
        {
            // Execute first sync immediately after delay
            await ExecuteSyncAsync(stoppingToken);

            // Then execute on interval
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteSyncAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("BlockchainSyncWorker is stopping gracefully");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "BlockchainSyncWorker encountered an unhandled exception");
            throw; // Re-throw to ensure the service crashes and can be restarted
        }
    }

    private async Task ExecuteSyncAsync(CancellationToken cancellationToken)
    {
        var syncStartTime = DateTime.UtcNow;
        _logger.LogInformation("Starting blockchain data synchronization at {Time} UTC", syncStartTime);

        try
        {
            // Create a new scope for scoped dependencies (DbContext, IRepository)
            using var scope = _serviceProvider.CreateScope();

            // Resolve IMediator from the scoped service provider
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // Send the sync command
            var result = await mediator.Send(new SyncBlockchainCommand(), cancellationToken);

            var duration = DateTime.UtcNow - syncStartTime;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Blockchain synchronization completed successfully. Records synced: {Count}, Duration: {Duration:F2}s",
                    result.Count,
                    duration.TotalSeconds);
            }
            else
            {
                _logger.LogWarning(
                    "Blockchain synchronization completed with no data. Duration: {Duration:F2}s",
                    duration.TotalSeconds);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("Blockchain synchronization cancelled");
            throw; // Propagate cancellation
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP error during blockchain synchronization. Check BlockCypher API connectivity");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to synchronize blockchain data. Next attempt in {Minutes} minutes",
                _settings.SyncIntervalMinutes);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BlockchainSyncWorker is stopping...");
        await base.StopAsync(cancellationToken);
        _logger.LogInformation("BlockchainSyncWorker stopped");
    }
}
