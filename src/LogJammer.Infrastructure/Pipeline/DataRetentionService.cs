using LogJammer.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class DataRetentionService(
    IServiceScopeFactory scopeFactory,
    ILogger<DataRetentionService> logger) : BackgroundService
{
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Data retention service started (retention period: {Days} days)", RetentionPeriod.TotalDays);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        // Run once on startup
        await RunRetentionAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunRetentionAsync(stoppingToken);
        }
    }

    private async Task RunRetentionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var occurrenceRepo = scope.ServiceProvider.GetRequiredService<IErrorOccurrenceRepository>();

            var cutoff = DateTime.UtcNow - RetentionPeriod;
            var deleted = await occurrenceRepo.DeleteOlderThanAsync(cutoff, cancellationToken);

            if (deleted > 0)
                logger.LogInformation("Data retention: deleted {Count} occurrence records older than {Cutoff}", deleted, cutoff);
            else
                logger.LogDebug("Data retention: no records to delete");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during data retention cleanup");
        }
    }
}
