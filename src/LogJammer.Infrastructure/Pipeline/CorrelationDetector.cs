using System.Text.Json;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class CorrelationDetector(
    IAlertRepository alertRepo,
    ICorrelatedSpikeAlertRepository correlatedRepo,
    ILogger<CorrelationDetector> logger) : ICorrelationDetector
{
    private const int MinGroupsForCorrelation = 3;

    public async Task DetectAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddMinutes(-5);
        var recentAlerts = await alertRepo.GetRecentByDataSourceAsync(dataSourceId, since, cancellationToken);

        if (recentAlerts.Count < MinGroupsForCorrelation)
            return;

        // Check for existing active correlated alert
        var existing = await correlatedRepo.GetActiveByDataSourceIdAsync(dataSourceId, cancellationToken);
        if (existing is not null)
            return;

        var alertIds = recentAlerts.Select(a => a.Id).ToList();
        var correlatedAlert = new CorrelatedSpikeAlert
        {
            DataSourceId = dataSourceId,
            Status = AlertStatus.Firing,
            AlertIds = JsonSerializer.Serialize(alertIds),
            GroupCount = recentAlerts.Count,
            DetectedAt = DateTime.UtcNow
        };

        await correlatedRepo.AddAsync(correlatedAlert, cancellationToken);
        logger.LogWarning("Correlated spike detected for DataSource {DataSourceId}: {GroupCount} groups spiking",
            dataSourceId, recentAlerts.Count);
    }
}
