using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using LogJammer.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogJammer.Tests.Unit;

public class SpikeDetectorTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private LogJammerDbContext _context = null!;
    private SpikeDetector _detector = null!;
    private DataSource _dataSource = null!;
    private KnownError _knownError = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _context = _fixture.CreateDbContext();
        await _context.Database.MigrateAsync();

        var occurrenceRepo = new ErrorOccurrenceRepository(_context);
        var ruleRepo = new SpikeDetectionRuleRepository(_context);
        _detector = new SpikeDetector(occurrenceRepo, ruleRepo, NullLogger<SpikeDetector>.Instance);

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
            FingerprintHash = "spike-test-hash",
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
    public async Task EvaluateAsync_AbsoluteThreshold_ReturnsSpike_WhenAboveThreshold()
    {
        // Create rule with low threshold
        _context.SpikeDetectionRules.Add(new SpikeDetectionRule
        {
            KnownErrorId = _knownError.Id,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 5,
            WindowMinutes = 5,
            LookbackMinutes = 1440,
            Enabled = true
        });
        await _context.SaveChangesAsync();

        // Create occurrences that exceed threshold
        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-3);
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = _knownError.Id,
            WindowStart = windowStart,
            WindowEnd = windowStart.AddMinutes(5),
            Count = 10
        });
        await _context.SaveChangesAsync();

        var result = await _detector.EvaluateAsync(_knownError.Id);

        result.Should().NotBeNull();
        result!.IsSpike.Should().BeTrue();
        result.ThresholdType.Should().Be(ThresholdType.Absolute);
        result.ActualValue.Should().Be(10);
    }

    [Fact]
    public async Task EvaluateAsync_AbsoluteThreshold_ReturnsNotSpike_WhenBelowThreshold()
    {
        _context.SpikeDetectionRules.Add(new SpikeDetectionRule
        {
            KnownErrorId = _knownError.Id,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 100,
            WindowMinutes = 5,
            LookbackMinutes = 1440,
            Enabled = true
        });
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = _knownError.Id,
            WindowStart = now.AddMinutes(-3),
            WindowEnd = now.AddMinutes(2),
            Count = 5
        });
        await _context.SaveChangesAsync();

        var result = await _detector.EvaluateAsync(_knownError.Id);

        result.Should().NotBeNull();
        result!.IsSpike.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_PercentageThreshold_ReturnsSpike_WhenPercentageExceeded()
    {
        _context.SpikeDetectionRules.Add(new SpikeDetectionRule
        {
            KnownErrorId = _knownError.Id,
            ThresholdType = ThresholdType.PercentageIncrease,
            ThresholdValue = 50,
            WindowMinutes = 5,
            LookbackMinutes = 30,
            Enabled = true
        });
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;

        // Historical baseline: 5 windows of 2 occurrences each (avg = 2)
        for (var i = 1; i <= 5; i++)
        {
            var start = now.AddMinutes(-5 * (i + 1));
            _context.ErrorOccurrences.Add(new ErrorOccurrence
            {
                KnownErrorId = _knownError.Id,
                WindowStart = start,
                WindowEnd = start.AddMinutes(5),
                Count = 2
            });
        }

        // Current window: 10 occurrences (400% increase from baseline of 2)
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = _knownError.Id,
            WindowStart = now.AddMinutes(-3),
            WindowEnd = now.AddMinutes(2),
            Count = 10
        });
        await _context.SaveChangesAsync();

        var result = await _detector.EvaluateAsync(_knownError.Id);

        result.Should().NotBeNull();
        result!.IsSpike.Should().BeTrue();
        result.ThresholdType.Should().Be(ThresholdType.PercentageIncrease);
    }

    [Fact]
    public async Task EvaluateAsync_PercentageThreshold_ReturnsNull_WhenNoHistoricalData()
    {
        _context.SpikeDetectionRules.Add(new SpikeDetectionRule
        {
            KnownErrorId = _knownError.Id,
            ThresholdType = ThresholdType.PercentageIncrease,
            ThresholdValue = 50,
            WindowMinutes = 5,
            LookbackMinutes = 30,
            Enabled = true
        });
        await _context.SaveChangesAsync();

        // Only current window, no historical data
        var now = DateTime.UtcNow;
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = _knownError.Id,
            WindowStart = now.AddMinutes(-3),
            WindowEnd = now.AddMinutes(2),
            Count = 10
        });
        await _context.SaveChangesAsync();

        var result = await _detector.EvaluateAsync(_knownError.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DisabledRule_ReturnsNull()
    {
        _context.SpikeDetectionRules.Add(new SpikeDetectionRule
        {
            KnownErrorId = _knownError.Id,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 1,
            WindowMinutes = 5,
            LookbackMinutes = 1440,
            Enabled = false
        });
        await _context.SaveChangesAsync();

        var result = await _detector.EvaluateAsync(_knownError.Id);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_FallsBackToGlobalDefault_WhenNoSpecificRule()
    {
        // Seed global default (no KnownErrorId)
        _context.SpikeDetectionRules.Add(new SpikeDetectionRule
        {
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 5,
            WindowMinutes = 5,
            LookbackMinutes = 1440,
            Enabled = true
        });
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = _knownError.Id,
            WindowStart = now.AddMinutes(-3),
            WindowEnd = now.AddMinutes(2),
            Count = 10
        });
        await _context.SaveChangesAsync();

        var result = await _detector.EvaluateAsync(_knownError.Id);

        result.Should().NotBeNull();
        result!.IsSpike.Should().BeTrue();
    }
}
