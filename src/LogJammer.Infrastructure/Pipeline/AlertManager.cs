using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class AlertManager(
    IAlertRepository alertRepo,
    ILogger<AlertManager> logger) : IAlertManager
{
    private const int MaxNotifications = 5;
    private static readonly TimeSpan MinNotificationInterval = TimeSpan.FromMinutes(10);
    private const int AutoResolveAfterWindows = 2;

    public async Task ProcessSpikeResultAsync(SpikeResult result, Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        var existingAlert = await alertRepo.GetActiveByKnownErrorIdAsync(result.KnownErrorId, cancellationToken);

        if (result.IsSpike)
        {
            if (existingAlert is null)
            {
                var alert = new Alert
                {
                    KnownErrorId = result.KnownErrorId,
                    Status = AlertStatus.Firing,
                    ThresholdType = result.ThresholdType,
                    ThresholdValue = result.ThresholdValue,
                    ActualValue = result.ActualValue,
                    NotificationCount = 1,
                    LastNotifiedAt = DateTime.UtcNow,
                    ConsecutiveBelowThreshold = 0
                };
                await alertRepo.AddAsync(alert, cancellationToken);
                logger.LogInformation("New alert created for KnownError {KnownErrorId}, actual={Actual}, threshold={Threshold}",
                    result.KnownErrorId, result.ActualValue, result.ThresholdValue);
            }
            else
            {
                existingAlert.ConsecutiveBelowThreshold = 0;
                existingAlert.ActualValue = result.ActualValue;
                await Escalate(existingAlert, cancellationToken);
            }
        }
        else if (existingAlert is not null)
        {
            existingAlert.ConsecutiveBelowThreshold++;

            if (existingAlert.ConsecutiveBelowThreshold >= AutoResolveAfterWindows)
            {
                existingAlert.Status = AlertStatus.Resolved;
                existingAlert.ResolvedAt = DateTime.UtcNow;
                logger.LogInformation("Auto-resolved alert {AlertId} for KnownError {KnownErrorId} after {Windows} consecutive below-threshold windows",
                    existingAlert.Id, existingAlert.KnownErrorId, AutoResolveAfterWindows);
            }

            await alertRepo.UpdateAsync(existingAlert, cancellationToken);
        }
    }

    public async Task AcknowledgeAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        var alert = await alertRepo.GetByIdAsync(alertId, cancellationToken)
            ?? throw new InvalidOperationException($"Alert {alertId} not found");

        alert.Status = AlertStatus.Acknowledged;
        alert.AcknowledgedAt = DateTime.UtcNow;
        await alertRepo.UpdateAsync(alert, cancellationToken);
    }

    public async Task ResolveAsync(Guid alertId, CancellationToken cancellationToken = default)
    {
        var alert = await alertRepo.GetByIdAsync(alertId, cancellationToken)
            ?? throw new InvalidOperationException($"Alert {alertId} not found");

        alert.Status = AlertStatus.Resolved;
        alert.ResolvedAt = DateTime.UtcNow;
        await alertRepo.UpdateAsync(alert, cancellationToken);
    }

    private async Task Escalate(Alert alert, CancellationToken cancellationToken)
    {
        if (alert.Status == AlertStatus.Acknowledged)
        {
            await alertRepo.UpdateAsync(alert, cancellationToken);
            return;
        }

        if (alert.NotificationCount >= MaxNotifications)
        {
            alert.Status = AlertStatus.FiringSuppressed;
            await alertRepo.UpdateAsync(alert, cancellationToken);
            return;
        }

        var now = DateTime.UtcNow;
        if (alert.LastNotifiedAt.HasValue && now - alert.LastNotifiedAt.Value < MinNotificationInterval)
        {
            await alertRepo.UpdateAsync(alert, cancellationToken);
            return;
        }

        alert.NotificationCount++;
        alert.LastNotifiedAt = now;
        await alertRepo.UpdateAsync(alert, cancellationToken);

        logger.LogInformation("Escalated alert {AlertId}, notification #{Count}", alert.Id, alert.NotificationCount);
    }
}
