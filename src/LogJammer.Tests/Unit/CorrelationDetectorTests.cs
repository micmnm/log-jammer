using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Infrastructure.Data;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using LogJammer.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogJammer.Tests.Unit;

public class CorrelationDetectorTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private LogJammerDbContext _context = null!;
    private CorrelationDetector _detector = null!;
    private DataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _context = _fixture.CreateDbContext();
        await _context.Database.MigrateAsync();

        var alertRepo = new AlertRepository(_context);
        var correlatedRepo = new CorrelatedSpikeAlertRepository(_context);
        _detector = new CorrelationDetector(alertRepo, correlatedRepo, NullLogger<CorrelationDetector>.Instance);

        _dataSource = new DataSource
        {
            Name = "Test Source",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}"
        };
        _context.DataSources.Add(_dataSource);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task DetectAsync_CreatesCorrelatedAlert_WhenThreeOrMoreGroupsSpike()
    {
        // Create 3 known errors with recent alerts
        for (var i = 0; i < 3; i++)
        {
            var knownError = new KnownError
            {
                FingerprintHash = $"corr-test-{i}",
                RepresentativeMessage = $"Test error {i}",
                DataSourceId = _dataSource.Id,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 1
            };
            _context.KnownErrors.Add(knownError);
            await _context.SaveChangesAsync();

            _context.Alerts.Add(new Alert
            {
                KnownErrorId = knownError.Id,
                Status = AlertStatus.Firing,
                ThresholdType = ThresholdType.Absolute,
                ThresholdValue = 10,
                ActualValue = 20
            });
        }
        await _context.SaveChangesAsync();

        await _detector.DetectAsync(_dataSource.Id);

        var correlated = await _context.CorrelatedSpikeAlerts.ToListAsync();
        correlated.Should().HaveCount(1);
        correlated[0].GroupCount.Should().Be(3);
        correlated[0].DataSourceId.Should().Be(_dataSource.Id);
    }

    [Fact]
    public async Task DetectAsync_DoesNotCreate_WhenFewerThanThreeGroups()
    {
        for (var i = 0; i < 2; i++)
        {
            var knownError = new KnownError
            {
                FingerprintHash = $"corr-few-{i}",
                RepresentativeMessage = $"Test error {i}",
                DataSourceId = _dataSource.Id,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 1
            };
            _context.KnownErrors.Add(knownError);
            await _context.SaveChangesAsync();

            _context.Alerts.Add(new Alert
            {
                KnownErrorId = knownError.Id,
                Status = AlertStatus.Firing,
                ThresholdType = ThresholdType.Absolute,
                ThresholdValue = 10,
                ActualValue = 20
            });
        }
        await _context.SaveChangesAsync();

        await _detector.DetectAsync(_dataSource.Id);

        var correlated = await _context.CorrelatedSpikeAlerts.ToListAsync();
        correlated.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectAsync_DedupesCorrelatedAlerts()
    {
        for (var i = 0; i < 3; i++)
        {
            var knownError = new KnownError
            {
                FingerprintHash = $"corr-dedup-{i}",
                RepresentativeMessage = $"Test error {i}",
                DataSourceId = _dataSource.Id,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 1
            };
            _context.KnownErrors.Add(knownError);
            await _context.SaveChangesAsync();

            _context.Alerts.Add(new Alert
            {
                KnownErrorId = knownError.Id,
                Status = AlertStatus.Firing,
                ThresholdType = ThresholdType.Absolute,
                ThresholdValue = 10,
                ActualValue = 20
            });
        }
        await _context.SaveChangesAsync();

        // Detect twice
        await _detector.DetectAsync(_dataSource.Id);
        await _detector.DetectAsync(_dataSource.Id);

        var correlated = await _context.CorrelatedSpikeAlerts.ToListAsync();
        correlated.Should().HaveCount(1);
    }
}
