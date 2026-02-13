using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LogJammer.Tests.Unit;

public class SpikeDetectorTests
{
    private readonly IErrorOccurrenceRepository _occurrenceRepo = Substitute.For<IErrorOccurrenceRepository>();
    private readonly ISpikeDetectionRuleRepository _ruleRepo = Substitute.For<ISpikeDetectionRuleRepository>();
    private readonly SpikeDetector _detector;
    private readonly Guid _knownErrorId = Guid.NewGuid();

    public SpikeDetectorTests()
    {
        _detector = new SpikeDetector(_occurrenceRepo, _ruleRepo, NullLogger<SpikeDetector>.Instance);
    }

    [Fact]
    public async Task EvaluateAsync_AbsoluteThreshold_ReturnsSpike_WhenAboveThreshold()
    {
        var rule = new SpikeDetectionRule
        {
            KnownErrorId = _knownErrorId,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 5,
            WindowMinutes = 5,
            LookbackMinutes = 1440,
            Enabled = true
        };
        _ruleRepo.GetByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(rule);

        _occurrenceRepo.GetByKnownErrorAsync(_knownErrorId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns([new ErrorOccurrence { KnownErrorId = _knownErrorId, Count = 10 }]);

        var result = await _detector.EvaluateAsync(_knownErrorId);

        result.Should().NotBeNull();
        result!.IsSpike.Should().BeTrue();
        result.ThresholdType.Should().Be(ThresholdType.Absolute);
        result.ActualValue.Should().Be(10);
    }

    [Fact]
    public async Task EvaluateAsync_AbsoluteThreshold_ReturnsNotSpike_WhenBelowThreshold()
    {
        var rule = new SpikeDetectionRule
        {
            KnownErrorId = _knownErrorId,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 100,
            WindowMinutes = 5,
            LookbackMinutes = 1440,
            Enabled = true
        };
        _ruleRepo.GetByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(rule);

        _occurrenceRepo.GetByKnownErrorAsync(_knownErrorId, Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<CancellationToken>())
            .Returns([new ErrorOccurrence { KnownErrorId = _knownErrorId, Count = 5 }]);

        var result = await _detector.EvaluateAsync(_knownErrorId);

        result.Should().NotBeNull();
        result!.IsSpike.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_PercentageThreshold_ReturnsSpike_WhenPercentageExceeded()
    {
        var rule = new SpikeDetectionRule
        {
            KnownErrorId = _knownErrorId,
            ThresholdType = ThresholdType.PercentageIncrease,
            ThresholdValue = 50,
            WindowMinutes = 5,
            LookbackMinutes = 30,
            Enabled = true
        };
        _ruleRepo.GetByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(rule);

        // Current window call (windowStart to null) returns 10
        _occurrenceRepo.GetByKnownErrorAsync(_knownErrorId, Arg.Any<DateTime?>(), Arg.Is<DateTime?>(d => d == null), Arg.Any<CancellationToken>())
            .Returns([new ErrorOccurrence { KnownErrorId = _knownErrorId, Count = 10 }]);

        // Historical call (lookbackStart to windowStart) returns baseline of 2 per window
        _occurrenceRepo.GetByKnownErrorAsync(_knownErrorId, Arg.Any<DateTime?>(), Arg.Is<DateTime?>(d => d != null), Arg.Any<CancellationToken>())
            .Returns([
                new ErrorOccurrence { Count = 2 },
                new ErrorOccurrence { Count = 2 },
                new ErrorOccurrence { Count = 2 },
                new ErrorOccurrence { Count = 2 },
                new ErrorOccurrence { Count = 2 }
            ]);

        var result = await _detector.EvaluateAsync(_knownErrorId);

        result.Should().NotBeNull();
        result!.IsSpike.Should().BeTrue();
        result.ThresholdType.Should().Be(ThresholdType.PercentageIncrease);
    }

    [Fact]
    public async Task EvaluateAsync_PercentageThreshold_ReturnsNull_WhenNoHistoricalData()
    {
        var rule = new SpikeDetectionRule
        {
            KnownErrorId = _knownErrorId,
            ThresholdType = ThresholdType.PercentageIncrease,
            ThresholdValue = 50,
            WindowMinutes = 5,
            LookbackMinutes = 30,
            Enabled = true
        };
        _ruleRepo.GetByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(rule);

        // Current window
        _occurrenceRepo.GetByKnownErrorAsync(_knownErrorId, Arg.Any<DateTime?>(), Arg.Is<DateTime?>(d => d == null), Arg.Any<CancellationToken>())
            .Returns([new ErrorOccurrence { KnownErrorId = _knownErrorId, Count = 10 }]);

        // No historical data
        _occurrenceRepo.GetByKnownErrorAsync(_knownErrorId, Arg.Any<DateTime?>(), Arg.Is<DateTime?>(d => d != null), Arg.Any<CancellationToken>())
            .Returns(new List<ErrorOccurrence>());

        var result = await _detector.EvaluateAsync(_knownErrorId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_DisabledRule_ReturnsNull()
    {
        var rule = new SpikeDetectionRule
        {
            KnownErrorId = _knownErrorId,
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 1,
            WindowMinutes = 5,
            LookbackMinutes = 1440,
            Enabled = false
        };
        _ruleRepo.GetByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns(rule);

        var result = await _detector.EvaluateAsync(_knownErrorId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task EvaluateAsync_FallsBackToGlobalDefault_WhenNoSpecificRule()
    {
        // No specific rule
        _ruleRepo.GetByKnownErrorIdAsync(_knownErrorId, Arg.Any<CancellationToken>())
            .Returns((SpikeDetectionRule?)null);

        var result = await _detector.EvaluateAsync(_knownErrorId);

        result.Should().BeNull();
    }
}
