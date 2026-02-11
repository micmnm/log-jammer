using FluentAssertions;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Infrastructure.Data;
using LogJammer.Infrastructure.Pipeline;
using LogJammer.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LogJammer.Tests.Integration;

public class SpikeDetectionIntegrationTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture = new();
    private LogJammerDbContext _context = null!;
    private SpikeDetector _spikeDetector = null!;
    private AlertManager _alertManager = null!;
    private CorrelationDetector _correlationDetector = null!;
    private DataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
        _context = _fixture.CreateDbContext();
        await _context.Database.MigrateAsync();

        var occurrenceRepo = new ErrorOccurrenceRepository(_context);
        var ruleRepo = new SpikeDetectionRuleRepository(_context);
        var alertRepo = new AlertRepository(_context);
        var correlatedRepo = new CorrelatedSpikeAlertRepository(_context);

        _spikeDetector = new SpikeDetector(occurrenceRepo, ruleRepo, NullLogger<SpikeDetector>.Instance);
        _alertManager = new AlertManager(alertRepo, NullLogger<AlertManager>.Instance);
        _correlationDetector = new CorrelationDetector(alertRepo, correlatedRepo, NullLogger<CorrelationDetector>.Instance);

        _dataSource = new DataSource
        {
            Name = "Integration Test Source",
            AdapterType = AdapterType.LogFile,
            ConnectionConfig = "{}"
        };
        _context.DataSources.Add(_dataSource);
        await _context.SaveChangesAsync();

        // Seed global rule
        _context.SpikeDetectionRules.Add(new SpikeDetectionRule
        {
            ThresholdType = ThresholdType.Absolute,
            ThresholdValue = 10,
            WindowMinutes = 5,
            LookbackMinutes = 1440,
            Enabled = true
        });
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _fixture.DisposeAsync();
    }

    [Fact]
    public async Task FullPipeline_DetectSpike_CreateAlert_AutoResolve()
    {
        var knownError = new KnownError
        {
            FingerprintHash = "integration-test-1",
            RepresentativeMessage = "Integration test error",
            DataSourceId = _dataSource.Id,
            FirstSeen = DateTime.UtcNow,
            LastSeen = DateTime.UtcNow,
            TotalOccurrences = 1
        };
        _context.KnownErrors.Add(knownError);
        await _context.SaveChangesAsync();

        var now = DateTime.UtcNow;

        // Create spike - 20 occurrences in the current window (threshold is 10)
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = knownError.Id,
            WindowStart = now.AddMinutes(-3),
            WindowEnd = now.AddMinutes(2),
            Count = 20
        });
        await _context.SaveChangesAsync();

        // Step 1: Detect spike
        var result = await _spikeDetector.EvaluateAsync(knownError.Id);
        result.Should().NotBeNull();
        result!.IsSpike.Should().BeTrue();

        // Step 2: Process spike → create alert
        await _alertManager.ProcessSpikeResultAsync(result, _dataSource.Id);
        var alert = await _context.Alerts.FirstOrDefaultAsync(a => a.KnownErrorId == knownError.Id);
        alert.Should().NotBeNull();
        alert!.Status.Should().Be(AlertStatus.Firing);

        // Step 3: Remove the spike data and add below-threshold data
        _context.ErrorOccurrences.RemoveRange(_context.ErrorOccurrences.Where(o => o.KnownErrorId == knownError.Id));
        _context.ErrorOccurrences.Add(new ErrorOccurrence
        {
            KnownErrorId = knownError.Id,
            WindowStart = now.AddMinutes(-3),
            WindowEnd = now.AddMinutes(2),
            Count = 2
        });
        await _context.SaveChangesAsync();

        // Two consecutive below-threshold evaluations
        for (var i = 0; i < 2; i++)
        {
            var belowResult = await _spikeDetector.EvaluateAsync(knownError.Id);
            belowResult.Should().NotBeNull();
            belowResult!.IsSpike.Should().BeFalse();
            await _alertManager.ProcessSpikeResultAsync(belowResult, _dataSource.Id);
        }

        // Step 4: Alert should be auto-resolved
        var resolvedAlert = await _context.Alerts.FirstAsync(a => a.Id == alert.Id);
        resolvedAlert.Status.Should().Be(AlertStatus.Resolved);
    }

    [Fact]
    public async Task FullPipeline_MultipleGroupSpikes_TriggersCorrelation()
    {
        var knownErrors = new List<KnownError>();
        var now = DateTime.UtcNow;

        for (var i = 0; i < 3; i++)
        {
            var error = new KnownError
            {
                FingerprintHash = $"corr-integration-{i}",
                RepresentativeMessage = $"Correlated error {i}",
                DataSourceId = _dataSource.Id,
                FirstSeen = DateTime.UtcNow,
                LastSeen = DateTime.UtcNow,
                TotalOccurrences = 1
            };
            _context.KnownErrors.Add(error);
            await _context.SaveChangesAsync();
            knownErrors.Add(error);

            // Add spike occurrences for each
            _context.ErrorOccurrences.Add(new ErrorOccurrence
            {
                KnownErrorId = error.Id,
                WindowStart = now.AddMinutes(-3),
                WindowEnd = now.AddMinutes(2),
                Count = 50
            });
        }
        await _context.SaveChangesAsync();

        // Evaluate and process all
        foreach (var error in knownErrors)
        {
            var result = await _spikeDetector.EvaluateAsync(error.Id);
            result.Should().NotBeNull();
            result!.IsSpike.Should().BeTrue();
            await _alertManager.ProcessSpikeResultAsync(result, _dataSource.Id);
        }

        // Run correlation detection
        await _correlationDetector.DetectAsync(_dataSource.Id);

        var correlated = await _context.CorrelatedSpikeAlerts.ToListAsync();
        correlated.Should().HaveCount(1);
        correlated[0].GroupCount.Should().Be(3);
    }
}
