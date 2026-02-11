using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Data;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using LogJammer.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogJammer.Tests.Unit;

public class AlertManagerTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private LogJammerDbContext _context = null!;
    private AlertManager _manager = null!;
    private AlertRepository _alertRepo = null!;
    private DataSource _dataSource = null!;
    private KnownError _knownError = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _context = _fixture.CreateDbContext();
        await _context.Database.MigrateAsync();

        _alertRepo = new AlertRepository(_context);
        _manager = new AlertManager(_alertRepo, NullLogger<AlertManager>.Instance);

        _dataSource = new DataSource
        {
            Name = "Test Source",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}"
        };
        _context.DataSources.Add(_dataSource);
        await _context.SaveChangesAsync();

        _knownError = new KnownError
        {
            FingerprintHash = "alert-test-hash",
            RepresentativeMessage = "Test error",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(_knownError);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task ProcessSpikeResult_CreatesNewAlert_WhenNoExistingAlert()
    {
        var result = new SpikeResult(_knownError.Id, ThresholdType.Absolute, 10, 15, true);

        await _manager.ProcessSpikeResultAsync(result, _dataSource.Id);

        var alerts = await _context.Alerts.Where(a => a.KnownErrorId == _knownError.Id).ToListAsync();
        alerts.Should().HaveCount(1);
        alerts[0].Status.Should().Be(AlertStatus.Firing);
        alerts[0].NotificationCount.Should().Be(1);
    }

    [Fact]
    public async Task ProcessSpikeResult_DoesNotDuplicate_WhenActiveAlertExists()
    {
        var result = new SpikeResult(_knownError.Id, ThresholdType.Absolute, 10, 15, true);

        await _manager.ProcessSpikeResultAsync(result, _dataSource.Id);
        await _manager.ProcessSpikeResultAsync(result, _dataSource.Id);

        var alerts = await _context.Alerts.Where(a => a.KnownErrorId == _knownError.Id).ToListAsync();
        alerts.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessSpikeResult_AutoResolves_After2ConsecutiveBelowThreshold()
    {
        // Create initial alert
        var spikeResult = new SpikeResult(_knownError.Id, ThresholdType.Absolute, 10, 15, true);
        await _manager.ProcessSpikeResultAsync(spikeResult, _dataSource.Id);

        // Two consecutive non-spike results
        var belowResult = new SpikeResult(_knownError.Id, ThresholdType.Absolute, 10, 5, false);
        await _manager.ProcessSpikeResultAsync(belowResult, _dataSource.Id);
        await _manager.ProcessSpikeResultAsync(belowResult, _dataSource.Id);

        var alert = await _context.Alerts.FirstAsync(a => a.KnownErrorId == _knownError.Id);
        alert.Status.Should().Be(AlertStatus.Resolved);
        alert.ResolvedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessSpikeResult_EscalationCapsAt5Notifications()
    {
        // Create initial alert with high notification count
        _context.Alerts.Add(new Alert
        {
            KnownErrorId = _knownError.Id,
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 15,
            NotificationCount = 5,
            LastNotifiedAt = DateTime.UtcNow.AddMinutes(-15)
        });
        await _context.SaveChangesAsync();

        var result = new SpikeResult(_knownError.Id, ThresholdType.Absolute, 10, 20, true);
        await _manager.ProcessSpikeResultAsync(result, _dataSource.Id);

        var alert = await _context.Alerts.FirstAsync(a => a.KnownErrorId == _knownError.Id);
        alert.Status.Should().Be(AlertStatus.FiringSuppressed);
        alert.NotificationCount.Should().Be(5);
    }

    [Fact]
    public async Task Acknowledge_StopsNotifications()
    {
        _context.Alerts.Add(new Alert
        {
            KnownErrorId = _knownError.Id,
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 15,
            NotificationCount = 2,
            LastNotifiedAt = DateTime.UtcNow.AddMinutes(-15)
        });
        await _context.SaveChangesAsync();

        var alert = await _context.Alerts.FirstAsync(a => a.KnownErrorId == _knownError.Id);
        await _manager.AcknowledgeAsync(alert.Id);

        var updated = await _context.Alerts.FirstAsync(a => a.Id == alert.Id);
        updated.Status.Should().Be(AlertStatus.Acknowledged);
        updated.AcknowledgedAt.Should().NotBeNull();

        // Further spike should not escalate
        var result = new SpikeResult(_knownError.Id, ThresholdType.Absolute, 10, 20, true);
        await _manager.ProcessSpikeResultAsync(result, _dataSource.Id);

        var afterEscalation = await _context.Alerts.FirstAsync(a => a.Id == alert.Id);
        afterEscalation.Status.Should().Be(AlertStatus.Acknowledged);
        afterEscalation.NotificationCount.Should().Be(2);
    }

    [Fact]
    public async Task ProcessSpikeResult_ResetsConsecutiveBelowThreshold_OnNewSpike()
    {
        var spikeResult = new SpikeResult(_knownError.Id, ThresholdType.Absolute, 10, 15, true);
        await _manager.ProcessSpikeResultAsync(spikeResult, _dataSource.Id);

        // One below threshold
        var belowResult = new SpikeResult(_knownError.Id, ThresholdType.Absolute, 10, 5, false);
        await _manager.ProcessSpikeResultAsync(belowResult, _dataSource.Id);

        var alert = await _context.Alerts.FirstAsync(a => a.KnownErrorId == _knownError.Id);
        alert.ConsecutiveBelowThreshold.Should().Be(1);

        // Spike again - should reset counter
        await _manager.ProcessSpikeResultAsync(spikeResult, _dataSource.Id);

        alert = await _context.Alerts.FirstAsync(a => a.KnownErrorId == _knownError.Id);
        alert.ConsecutiveBelowThreshold.Should().Be(0);
        alert.Status.Should().NotBe(AlertStatus.Resolved);
    }
}
