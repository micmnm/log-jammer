using LogJammer.Engine.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.BackgroundServices;

public class DataRetentionService(IServiceScopeFactory scopeFactory, ILogger<DataRetentionService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();

                var cutoff = DateTimeOffset.UtcNow.AddDays(-42); // 6 weeks

                // Delete old occurrences
                var deletedOccurrences = await db.PatternOccurrences
                    .Where(o => o.WindowStart < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                // Delete stale patterns: no occurrences in 6 weeks and not new
                var deletedPatterns = await db.LogPatterns
                    .Where(p => !p.IsNew && p.LastSeen < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                logger.LogInformation(
                    "Data retention: deleted {Occurrences} occurrences, {Patterns} stale patterns",
                    deletedOccurrences,
                    deletedPatterns);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during data retention");
            }

            await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
        }
    }
}
