using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LogJammer.Tests.Unit;

public class AlertManagerTests
{
    private readonly IAlertRepository _alertRepo = Substitute.For<IAlertRepository>();
    private readonly AlertManager _manager;
    private readonly Guid _knownErrorId = Guid.NewGuid();
    private readonly Guid _dataSourceId = Guid.NewGuid();

    public AlertManagerTests()
    {
        _manager = new AlertManager(_alertRepo, NullLogger<AlertManager>.Instance);
    }

    [Fact]
    public async Task ProcessSpikeResult_CreatesNewAlert_WhenNoExistingAlert()
    {
        _alertRepo.GetActiveByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns((Alert?)null);

        Alert? captured = null;
        _alertRepo.AddAsync(Arg.Any<Alert>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured = ci.Arg<Alert>();
                return captured;
            });

        var result = new SpikeResult(_knownErrorId, ThresholdType.Absolute, 10, 15, true);
        await _manager.ProcessSpikeResultAsync(result, _dataSourceId);

        await _alertRepo.Received(1).AddAsync(Arg.Any<Alert>(), Arg.Any<CancellationToken>());
        captured.Should().NotBeNull();
        captured!.Status.Should().Be(AlertStatus.Firing);
        captured.NotificationCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessSpikeResult_DoesNotDuplicate_WhenActiveAlertExists()
    {
        var existingAlert = new Alert
        {
            Id = Guid.NewGuid(),
            KnownErrorId = _knownErrorId,
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 15,
            NotificationCount = 1,
            LastNotifiedAt = DateTime.UtcNow.AddMinutes(-15),
            ConsecutiveBelowThreshold = 0
        };
        _alertRepo.GetActiveByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(existingAlert);

        var result = new SpikeResult(_knownErrorId, ThresholdType.Absolute, 10, 15, true);
        await _manager.ProcessSpikeResultAsync(result, _dataSourceId);
        await _manager.ProcessSpikeResultAsync(result, _dataSourceId);

        await _alertRepo.DidNotReceive().AddAsync(Arg.Any<Alert>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessSpikeResult_AutoResolves_After2ConsecutiveBelowThreshold()
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            KnownErrorId = _knownErrorId,
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 15,
            NotificationCount = 1,
            ConsecutiveBelowThreshold = 0
        };
        _alertRepo.GetActiveByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(alert);

        var belowResult = new SpikeResult(_knownErrorId, ThresholdType.Absolute, 10, 5, false);

        await _manager.ProcessSpikeResultAsync(belowResult, _dataSourceId);
        alert.ConsecutiveBelowThreshold.Should().Be(1);

        await _manager.ProcessSpikeResultAsync(belowResult, _dataSourceId);
        alert.Status.Should().Be(AlertStatus.Resolved);
        alert.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessSpikeResult_EscalationCapsAt5Notifications()
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            KnownErrorId = _knownErrorId,
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 15,
            NotificationCount = 5,
            LastNotifiedAt = DateTime.UtcNow.AddMinutes(-15),
            ConsecutiveBelowThreshold = 0
        };
        _alertRepo.GetActiveByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(alert);

        var result = new SpikeResult(_knownErrorId, ThresholdType.Absolute, 10, 20, true);
        await _manager.ProcessSpikeResultAsync(result, _dataSourceId);

        alert.Status.Should().Be(AlertStatus.FiringSuppressed);
        alert.NotificationCount.Should().Be(5);
    }

    [Fact]
    public async Task Acknowledge_StopsNotifications()
    {
        var alertId = Guid.NewGuid();
        var alert = new Alert
        {
            Id = alertId,
            KnownErrorId = _knownErrorId,
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 15,
            NotificationCount = 2,
            LastNotifiedAt = DateTime.UtcNow.AddMinutes(-15),
            ConsecutiveBelowThreshold = 0
        };
        _alertRepo.GetByIdAsync(alertId, Arg.Any<CancellationToken>())
            .Returns(alert);
        _alertRepo.GetActiveByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(alert);

        await _manager.AcknowledgeAsync(alertId);

        alert.Status.Should().Be(AlertStatus.Acknowledged);
        alert.AcknowledgedAt.Should().NotBeNull();

        // Further spike should not escalate
        var result = new SpikeResult(_knownErrorId, ThresholdType.Absolute, 10, 20, true);
        await _manager.ProcessSpikeResultAsync(result, _dataSourceId);

        alert.Status.Should().Be(AlertStatus.Acknowledged);
        alert.NotificationCount.Should().Be(2);
    }

    [Fact]
    public async Task ProcessSpikeResult_ResetsConsecutiveBelowThreshold_OnNewSpike()
    {
        var alert = new Alert
        {
            Id = Guid.NewGuid(),
            KnownErrorId = _knownErrorId,
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 15,
            NotificationCount = 1,
            LastNotifiedAt = DateTime.UtcNow.AddMinutes(-15),
            ConsecutiveBelowThreshold = 0
        };
        _alertRepo.GetActiveByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(alert);

        // One below threshold
        var belowResult = new SpikeResult(_knownErrorId, ThresholdType.Absolute, 10, 5, false);
        await _manager.ProcessSpikeResultAsync(belowResult, _dataSourceId);
        alert.ConsecutiveBelowThreshold.Should().Be(1);

        // Spike again - should reset counter
        var spikeResult = new SpikeResult(_knownErrorId, ThresholdType.Absolute, 10, 15, true);
        await _manager.ProcessSpikeResultAsync(spikeResult, _dataSourceId);

        alert.ConsecutiveBelowThreshold.Should().Be(0);
        alert.Status.Should().NotBe(AlertStatus.Resolved);
    }
}
