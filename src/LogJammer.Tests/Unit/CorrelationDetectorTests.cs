using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LogJammer.Tests.Unit;

public class CorrelationDetectorTests
{
    private readonly IAlertRepository _alertRepo = Substitute.For<IAlertRepository>();
    private readonly ICorrelatedSpikeAlertRepository _correlatedRepo = Substitute.For<ICorrelatedSpikeAlertRepository>();
    private readonly CorrelationDetector _detector;
    private readonly Guid _dataSourceId = Guid.NewGuid();

    public CorrelationDetectorTests()
    {
        _detector = new CorrelationDetector(_alertRepo, _correlatedRepo, NullLogger<CorrelationDetector>.Instance);
    }

    [Fact]
    public async Task DetectAsync_CreatesCorrelatedAlert_WhenThreeOrMoreGroupsSpike()
    {
        var alerts = Enumerable.Range(0, 3).Select(i => new Alert
        {
            Id = Guid.NewGuid(),
            KnownErrorId = Guid.NewGuid(),
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 20
        }).ToList();

        _alertRepo.GetRecentByDataSourceAsync(_dataSourceId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(alerts);
        _correlatedRepo.GetActiveByDataSourceIdAsync(_dataSourceId, Arg.Any<CancellationToken>())
            .Returns((CorrelatedSpikeAlert?)null);

        CorrelatedSpikeAlert? captured = null;
        _correlatedRepo.AddAsync(Arg.Any<CorrelatedSpikeAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                captured = ci.Arg<CorrelatedSpikeAlert>();
                return captured;
            });

        await _detector.DetectAsync(_dataSourceId);

        await _correlatedRepo.Received(1).AddAsync(Arg.Any<CorrelatedSpikeAlert>(), Arg.Any<CancellationToken>());
        captured.Should().NotBeNull();
        captured!.GroupCount.Should().Be(3);
        captured.DataSourceId.Should().Be(_dataSourceId);
    }

    [Fact]
    public async Task DetectAsync_DoesNotCreate_WhenFewerThanThreeGroups()
    {
        var alerts = Enumerable.Range(0, 2).Select(i => new Alert
        {
            Id = Guid.NewGuid(),
            KnownErrorId = Guid.NewGuid(),
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 20
        }).ToList();

        _alertRepo.GetRecentByDataSourceAsync(_dataSourceId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(alerts);

        await _detector.DetectAsync(_dataSourceId);

        await _correlatedRepo.DidNotReceive().AddAsync(Arg.Any<CorrelatedSpikeAlert>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DetectAsync_DedupesCorrelatedAlerts()
    {
        var alerts = Enumerable.Range(0, 3).Select(i => new Alert
        {
            Id = Guid.NewGuid(),
            KnownErrorId = Guid.NewGuid(),
            Status = AlertStatus.Firing,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            ActualValue = 20
        }).ToList();

        _alertRepo.GetRecentByDataSourceAsync(_dataSourceId, Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(alerts);

        // First call: no existing correlated alert
        _correlatedRepo.GetActiveByDataSourceIdAsync(_dataSourceId, Arg.Any<CancellationToken>())
            .Returns(
                (CorrelatedSpikeAlert?)null,
                new CorrelatedSpikeAlert { DataSourceId = _dataSourceId, Status = AlertStatus.Firing, GroupCount = 3 });

        _correlatedRepo.AddAsync(Arg.Any<CorrelatedSpikeAlert>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<CorrelatedSpikeAlert>());

        await _detector.DetectAsync(_dataSourceId);
        await _detector.DetectAsync(_dataSourceId);

        await _correlatedRepo.Received(1).AddAsync(Arg.Any<CorrelatedSpikeAlert>(), Arg.Any<CancellationToken>());
    }
}
