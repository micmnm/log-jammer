using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class SpikeDetectionProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<SpikeDetectionProcessor> logger) : BackgroundService
{
    private const int PollIntervalSeconds = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Spike detection processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in spike detection processor");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Spike detection processor stopped");
    }

    private async Task ProcessCycleAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
        var spikeDetector = scope.ServiceProvider.GetRequiredService<ISpikeDetector>();
        var alertManager = scope.ServiceProvider.GetRequiredService<IAlertManager>();
        var correlationDetector = scope.ServiceProvider.GetRequiredService<ICorrelationDetector>();

        var knownErrors = await context.KnownErrors
            .Where(e => e.Status == Core.Enums.ErrorStatus.Active)
            .Select(e => new { e.Id, e.DataSourceId })
            .AsNoTracking()
            .ToListAsync(ct);

        if (knownErrors.Count == 0) return;

        logger.LogDebug("Evaluating {Count} known errors for spikes", knownErrors.Count);

        var dataSourcesWithSpikes = new HashSet<Guid>();

        foreach (var error in knownErrors)
        {
            try
            {
                var result = await spikeDetector.EvaluateAsync(error.Id, ct);
                if (result is not null)
                {
                    await alertManager.ProcessSpikeResultAsync(result, error.DataSourceId, ct);
                    if (result.IsSpike)
                        dataSourcesWithSpikes.Add(error.DataSourceId);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to evaluate spike for KnownError {KnownErrorId}", error.Id);
            }
        }

        // Run correlation detection for data sources that had new spikes
        foreach (var dataSourceId in dataSourcesWithSpikes)
        {
            try
            {
                await correlationDetector.DetectAsync(dataSourceId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to run correlation detection for DataSource {DataSourceId}", dataSourceId);
            }
        }
    }
}
