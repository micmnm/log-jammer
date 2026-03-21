using LogJammer.Engine;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Processing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LogJammer.Tests.Processing;

[Collection("Database")]
public class DataFlowTests(DatabaseFixture db)
{
    private IngestionPipeline CreatePipeline()
    {
        var services = new ServiceCollection();
        services.AddDbContext<LogJammerDbContext>(options =>
            options.UseNpgsql(db.ConnectionString));
        var sp = services.BuildServiceProvider();
        return new IngestionPipeline(sp.GetRequiredService<IServiceScopeFactory>());
    }

    private async Task<DataSource> CreateDataSourceAsync(string name = "test")
    {
        var ds = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"{name}-{Guid.NewGuid():N}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = "{}",
        };
        await using var ctx = db.CreateDbContext();
        ctx.DataSources.Add(ds);
        await ctx.SaveChangesAsync();
        return ds;
    }

    // ── Occurrence counting ──────────────────────────────────────

    [Fact]
    public async Task MultipleEntries_SameHour_IncrementOccurrenceCount()
    {
        var ds = await CreateDataSourceAsync("same-hour");
        var pipeline = CreatePipeline();
        var now = DateTimeOffset.UtcNow;

        var entries = Enumerable.Range(0, 5).Select(_ => new RawLogEntry
        {
            Message = "Connection refused to database host",
            Timestamp = now,
            Level = "error",
        });

        await pipeline.ProcessEntriesAsync(entries, ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var occurrence = await ctx.PatternOccurrences
            .Where(o => o.Pattern.DataSourceId == ds.Id)
            .SingleAsync();

        Assert.Equal(5, occurrence.Count);
    }

    [Fact]
    public async Task Entries_DifferentHours_CreateSeparateOccurrenceWindows()
    {
        var ds = await CreateDataSourceAsync("diff-hours");
        var pipeline = CreatePipeline();

        var hour1 = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);
        var hour2 = new DateTimeOffset(2026, 3, 20, 11, 0, 0, TimeSpan.Zero);

        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "Disk space low on server", Timestamp = hour1, Level = "warn" },
            new RawLogEntry { Message = "Disk space low on server", Timestamp = hour2, Level = "warn" },
        ], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var occurrences = await ctx.PatternOccurrences
            .Where(o => o.Pattern.DataSourceId == ds.Id)
            .OrderBy(o => o.WindowStart)
            .ToListAsync();

        Assert.Equal(2, occurrences.Count);
        Assert.Equal(hour1, occurrences[0].WindowStart);
        Assert.Equal(hour2, occurrences[1].WindowStart);
        Assert.All(occurrences, o => Assert.Equal(1, o.Count));
    }

    // ── Severity mapping through pipeline ────────────────────────

    [Theory]
    [InlineData("debug", Severity.Info)]
    [InlineData("WARN", Severity.Warning)]
    [InlineData("error", Severity.Error)]
    [InlineData("FATAL", Severity.Critical)]
    [InlineData(null, Severity.Info)]
    [InlineData("unknown-level", Severity.Info)]
    public async Task SeverityMapping_FlowsThroughToPattern(string? level, Severity expected)
    {
        var ds = await CreateDataSourceAsync("severity");
        var pipeline = CreatePipeline();

        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry
            {
                Message = $"Test severity {Guid.NewGuid()}",
                Timestamp = DateTimeOffset.UtcNow,
                Level = level,
            }
        ], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);
        Assert.Equal(expected, pattern.Severity);
    }

    // ── Message template + fields flow ───────────────────────────

    [Fact]
    public async Task MessageTemplate_AppliedWithFields()
    {
        var ds = await CreateDataSourceAsync("template");
        var pipeline = CreatePipeline();

        var entry = new RawLogEntry
        {
            Message = "raw message ignored when template+fields present",
            Timestamp = DateTimeOffset.UtcNow,
            Level = "info",
            Fields = new Dictionary<string, string>
            {
                ["userId"] = "42",
                ["action"] = "login",
            },
        };

        await pipeline.ProcessEntriesAsync([entry], ds.Id, "User {userId} performed {action}");

        await using var ctx = db.CreateDbContext();
        var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);
        Assert.Contains("42", pattern.SampleMessage);
        Assert.Contains("login", pattern.SampleMessage);
    }

    [Fact]
    public async Task MessageTemplate_MissingFieldsLeftAsPlaceholder()
    {
        var ds = await CreateDataSourceAsync("template-missing");
        var pipeline = CreatePipeline();

        var entry = new RawLogEntry
        {
            Message = "fallback",
            Timestamp = DateTimeOffset.UtcNow,
            Level = "info",
            Fields = new Dictionary<string, string> { ["userId"] = "99" },
        };

        await pipeline.ProcessEntriesAsync([entry], ds.Id, "User {userId} from {ip}");

        await using var ctx = db.CreateDbContext();
        var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);
        Assert.Contains("99", pattern.SampleMessage);
        Assert.Contains("{ip}", pattern.SampleMessage);
    }

    [Fact]
    public async Task NoTemplate_UsesRawMessage()
    {
        var ds = await CreateDataSourceAsync("no-template");
        var pipeline = CreatePipeline();

        var entry = new RawLogEntry
        {
            Message = "raw log message with id 12345",
            Timestamp = DateTimeOffset.UtcNow,
            Level = "info",
            Fields = new Dictionary<string, string> { ["unused"] = "field" },
        };

        // messageTemplate is null → Fields ignored, raw message used
        await pipeline.ProcessEntriesAsync([entry], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);
        Assert.Equal("raw log message with id 12345", pattern.SampleMessage);
    }

    // ── Stack trace preprocessing ────────────────────────────────

    [Fact]
    public async Task StackTraceFields_NormalizedBeforeTemplateApply()
    {
        var ds = await CreateDataSourceAsync("stacktrace");
        var pipeline = CreatePipeline();

        var entry = new RawLogEntry
        {
            Message = "error occurred",
            Timestamp = DateTimeOffset.UtcNow,
            Level = "error",
            Fields = new Dictionary<string, string>
            {
                ["message"] = "NullRef",
                ["stackTrace"] = """
                    at MyApp.Services.PaymentService.Process(PaymentRequest req) in /app/src/Pay.cs:line 42
                    at MyApp.Infrastructure.DatabaseClient.Execute(string sql) in /app/src/Db.cs:line 10
                    at MyApp.Core.Handler.Handle(Command cmd) in /app/src/Handler.cs:line 5
                    at MyApp.Startup.Run() in /app/src/Startup.cs:line 1
                    """,
            },
        };

        await pipeline.ProcessEntriesAsync([entry], ds.Id, "{message}: {stackTrace}");

        await using var ctx = db.CreateDbContext();
        var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);

        // Stack trace should be shortened to top 3 frames
        Assert.Contains("PaymentService.Process", pattern.SampleMessage);
        Assert.Contains("DatabaseClient.Execute", pattern.SampleMessage);
        Assert.Contains("Handler.Handle", pattern.SampleMessage);
        // 4th frame excluded
        Assert.DoesNotContain("Startup.Run", pattern.SampleMessage);
    }

    // ── Drain clustering / pattern merging ───────────────────────

    [Fact]
    public async Task SimilarMessages_ClusterIntoSamePattern()
    {
        var ds = await CreateDataSourceAsync("cluster");
        var pipeline = CreatePipeline();
        var now = DateTimeOffset.UtcNow;

        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "Connection to db-primary:5432 timed out after 3000ms", Timestamp = now, Level = "error" },
            new RawLogEntry { Message = "Connection to db-replica:5432 timed out after 5000ms", Timestamp = now, Level = "error" },
        ], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var patterns = await ctx.LogPatterns.Where(p => p.DataSourceId == ds.Id).ToListAsync();

        // Both should cluster into one pattern with wildcards
        Assert.Single(patterns);
        Assert.Contains("*", patterns[0].Template);
    }

    [Fact]
    public async Task DifferentMessages_CreateSeparatePatterns()
    {
        var ds = await CreateDataSourceAsync("separate");
        var pipeline = CreatePipeline();
        var now = DateTimeOffset.UtcNow;

        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "User authentication failed for admin", Timestamp = now, Level = "warn" },
            new RawLogEntry { Message = "Disk usage exceeded threshold on volume sda1", Timestamp = now, Level = "error" },
        ], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var patterns = await ctx.LogPatterns.Where(p => p.DataSourceId == ds.Id).ToListAsync();

        Assert.Equal(2, patterns.Count);
    }

    // ── DrainState persistence across pipeline instances ─────────

    [Fact]
    public async Task DrainState_PersistedAndRestoredAcrossPipelineInstances()
    {
        var ds = await CreateDataSourceAsync("drain-state");
        var pipeline1 = CreatePipeline();
        var now = DateTimeOffset.UtcNow;

        // First pipeline: create a cluster
        await pipeline1.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "Connection to db-primary:5432 timed out after 3000ms", Timestamp = now, Level = "error" },
        ], ds.Id, null);

        // Second pipeline (simulates app restart): should restore state and merge into same cluster
        var pipeline2 = CreatePipeline();
        await pipeline2.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "Connection to db-standby:5432 timed out after 1000ms", Timestamp = now, Level = "error" },
        ], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var patterns = await ctx.LogPatterns.Where(p => p.DataSourceId == ds.Id).ToListAsync();

        // Both messages should map to the same cluster thanks to restored DrainState
        Assert.Single(patterns);
    }

    // ── Pattern metadata updates ─────────────────────────────────

    [Fact]
    public async Task SubsequentOccurrence_UpdatesLastSeenAndSampleMessage()
    {
        var ds = await CreateDataSourceAsync("metadata");
        var pipeline = CreatePipeline();

        var t1 = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 3, 20, 14, 0, 0, TimeSpan.Zero);

        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "Connection to db-primary:5432 timed out after 3000ms", Timestamp = t1, Level = "error" },
        ], ds.Id, null);

        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "Connection to db-replica:5432 timed out after 5000ms", Timestamp = t2, Level = "error" },
        ], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);

        Assert.Equal(t1, pattern.FirstSeen);
        Assert.Equal(t2, pattern.LastSeen);
        // SampleMessage updates to latest
        Assert.Contains("replica", pattern.SampleMessage);
    }

    [Fact]
    public async Task NewPattern_IsNew_UntilAcknowledged()
    {
        var ds = await CreateDataSourceAsync("isnew");
        var pipeline = CreatePipeline();

        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "First pattern seen here", Timestamp = DateTimeOffset.UtcNow, Level = "info" },
        ], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);
        Assert.True(pattern.IsNew);

        // Acknowledge
        var store = new PatternStore(ctx);
        var result = await store.AcknowledgeAsync(pattern.Id);

        await using var readCtx = db.CreateDbContext();
        var acked = await readCtx.LogPatterns.SingleAsync(p => p.Id == pattern.Id);
        Assert.False(acked.IsNew);
    }

    // ── Acknowledge cascades to similar patterns ─────────────────

    [Fact]
    public async Task Acknowledge_CascadesToSimilarPatterns()
    {
        var ds = await CreateDataSourceAsync("ack-cascade");
        var pipeline = CreatePipeline();
        var now = DateTimeOffset.UtcNow;

        // Create two similar but different patterns (will cluster separately due to different structure)
        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "Error connecting to primary database server", Timestamp = now, Level = "error" },
        ], ds.Id, null);

        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry { Message = "Error connecting to replica database server", Timestamp = now, Level = "error" },
        ], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var patterns = await ctx.LogPatterns.Where(p => p.DataSourceId == ds.Id).ToListAsync();

        // If they clustered into one pattern, acknowledge that one
        // If they're separate, acknowledge one and check if cascade applies
        if (patterns.Count == 1)
        {
            var store = new PatternStore(ctx);
            await store.AcknowledgeAsync(patterns[0].Id);

            await using var readCtx = db.CreateDbContext();
            var p = await readCtx.LogPatterns.SingleAsync(lp => lp.Id == patterns[0].Id);
            Assert.False(p.IsNew);
        }
        else
        {
            Assert.True(patterns.All(p => p.IsNew));

            var store = new PatternStore(ctx);
            var result = await store.AcknowledgeAsync(patterns[0].Id);

            // Similar patterns should be cascade-acknowledged
            await using var readCtx = db.CreateDbContext();
            var all = await readCtx.LogPatterns.Where(p => p.DataSourceId == ds.Id).ToListAsync();
            Assert.True(all.All(p => !p.IsNew), "Similar patterns should be cascade-acknowledged");
            Assert.True(result.SimilarCount > 0);
        }
    }

    [Fact]
    public async Task Acknowledge_DoesNotCascadeAcrossDataSources()
    {
        var ds1 = await CreateDataSourceAsync("ack-ds1");
        var ds2 = await CreateDataSourceAsync("ack-ds2");
        var pipeline = CreatePipeline();
        var now = DateTimeOffset.UtcNow;

        // Same message in two different data sources
        await pipeline.ProcessEntriesAsync(
            [new RawLogEntry { Message = "Timeout waiting for API response", Timestamp = now, Level = "error" }],
            ds1.Id, null);

        await pipeline.ProcessEntriesAsync(
            [new RawLogEntry { Message = "Timeout waiting for API response", Timestamp = now, Level = "error" }],
            ds2.Id, null);

        await using var ctx = db.CreateDbContext();
        var pattern1 = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds1.Id);
        var store = new PatternStore(ctx);
        await store.AcknowledgeAsync(pattern1.Id);

        await using var readCtx = db.CreateDbContext();
        var pattern2 = await readCtx.LogPatterns.SingleAsync(p => p.DataSourceId == ds2.Id);
        Assert.True(pattern2.IsNew, "Acknowledge should not cross data source boundaries");
    }

    [Fact]
    public async Task AcknowledgeAll_ScopedToDataSource()
    {
        var ds1 = await CreateDataSourceAsync("ackall-ds1");
        var ds2 = await CreateDataSourceAsync("ackall-ds2");
        var pipeline = CreatePipeline();
        var now = DateTimeOffset.UtcNow;

        await pipeline.ProcessEntriesAsync(
            [new RawLogEntry { Message = "Error in service alpha", Timestamp = now, Level = "error" }],
            ds1.Id, null);
        await pipeline.ProcessEntriesAsync(
            [new RawLogEntry { Message = "Error in service beta", Timestamp = now, Level = "error" }],
            ds2.Id, null);

        await using var ctx = db.CreateDbContext();
        var store = new PatternStore(ctx);
        var count = await store.AcknowledgeAllAsync(ds1.Id);

        Assert.True(count >= 1);

        await using var readCtx = db.CreateDbContext();
        var p1 = await readCtx.LogPatterns.SingleAsync(p => p.DataSourceId == ds1.Id);
        var p2 = await readCtx.LogPatterns.SingleAsync(p => p.DataSourceId == ds2.Id);
        Assert.False(p1.IsNew);
        Assert.True(p2.IsNew, "AcknowledgeAll with dataSourceId should not affect other data sources");
    }

    // ── End-to-end: Ingest → Baseline ────────────────────────────

    [Fact]
    public async Task EndToEnd_IngestToBaselineComparison()
    {
        var ds = await CreateDataSourceAsync("e2e");
        var pipeline = CreatePipeline();

        // Ingest entries across 4 weeks, same hour-of-week
        var now = DateTimeOffset.UtcNow;
        var baseHour = new DateTimeOffset(
            now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day,
            now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);

        // Use multiples of 7 so all land on same day-of-week as today (same HourOfWeek)
        var offsets = new[] { -7, -14, -21 };
        foreach (var dayOffset in offsets)
        {
            var timestamp = baseHour.AddDays(dayOffset);
            var entries = Enumerable.Range(0, 10).Select(_ => new RawLogEntry
            {
                Message = "Payment processing failed for order 12345",
                Timestamp = timestamp,
                Level = "error",
            });
            await pipeline.ProcessEntriesAsync(entries, ds.Id, null);
        }

        // Verify occurrences were created
        await using (var ctx = db.CreateDbContext())
        {
            var occurrences = await ctx.PatternOccurrences
                .Where(o => o.Pattern.DataSourceId == ds.Id)
                .ToListAsync();

            Assert.Equal(3, occurrences.Count);
            Assert.All(occurrences, o => Assert.Equal(10, o.Count));
        }

        // Recalculate baseline
        await using (var ctx = db.CreateDbContext())
        {
            var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);
            var calculator = new BaselineCalculator(ctx);
            await calculator.RecalculateBaselinesAsync(pattern.Id);
        }

        // Verify baseline was created for the current hour-of-week
        var currentHourOfWeek = (int)now.UtcDateTime.DayOfWeek * 24 + now.UtcDateTime.Hour;
        await using (var ctx = db.CreateDbContext())
        {
            var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);
            var baseline = await ctx.PatternBaselines
                .FirstOrDefaultAsync(b => b.PatternId == pattern.Id && b.HourOfWeek == currentHourOfWeek);

            Assert.NotNull(baseline);
            Assert.Equal(10.0, baseline.AvgCount, precision: 6);
            // All counts identical → stddev = 0
            Assert.Equal(0.0, baseline.StdDevCount, precision: 6);
        }

        // Now ingest a spike in the current hour
        var spikeEntries = Enumerable.Range(0, 50).Select(_ => new RawLogEntry
        {
            Message = "Payment processing failed for order 67890",
            Timestamp = now,
            Level = "error",
        });
        await pipeline.ProcessEntriesAsync(spikeEntries, ds.Id, null);

        // Compare against baseline — current rate should be 50, expected 10
        await using (var ctx = db.CreateDbContext())
        {
            var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);
            var calculator = new BaselineCalculator(ctx);
            var comparison = await calculator.GetCurrentComparisonAsync(pattern.Id);

            Assert.NotNull(comparison);
            Assert.Equal(50, comparison.CurrentRate);
            Assert.Equal(10.0, comparison.ExpectedRate, precision: 6);
            // StdDev = 0, so StdDevsFromMean = 0 (can't compute deviation with zero variance)
            Assert.Equal(0.0, comparison.StdDevsFromMean, precision: 6);
        }
    }

    // ── Baseline edge cases ──────────────────────────────────────

    [Fact]
    public async Task Baseline_NoOccurrenceInCurrentHour_ReturnsZeroRate()
    {
        var ds = await CreateDataSourceAsync("baseline-zero");

        var pattern = new LogPattern
        {
            Id = Guid.NewGuid(),
            Template = "Some pattern *",
            ClusterId = 999,
            FirstSeen = DateTimeOffset.UtcNow.AddDays(-7),
            LastSeen = DateTimeOffset.UtcNow.AddDays(-1),
            SampleMessage = "Some pattern abc",
            Severity = Severity.Info,
            DataSourceId = ds.Id,
        };

        var now = DateTimeOffset.UtcNow;
        var hourOfWeek = (int)now.UtcDateTime.DayOfWeek * 24 + now.UtcDateTime.Hour;

        var baseline = new PatternBaseline
        {
            Id = Guid.NewGuid(),
            PatternId = pattern.Id,
            HourOfWeek = hourOfWeek,
            AvgCount = 20.0,
            StdDevCount = 5.0,
        };

        await using (var ctx = db.CreateDbContext())
        {
            ctx.LogPatterns.Add(pattern);
            ctx.PatternBaselines.Add(baseline);
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = db.CreateDbContext())
        {
            var calculator = new BaselineCalculator(ctx);
            var comparison = await calculator.GetCurrentComparisonAsync(pattern.Id);

            Assert.NotNull(comparison);
            Assert.Equal(0, comparison.CurrentRate);
            Assert.Equal(20.0, comparison.ExpectedRate, precision: 6);
            // (0 - 20) / 5 = -4.0
            Assert.Equal(-4.0, comparison.StdDevsFromMean, precision: 6);
        }
    }

    [Fact]
    public async Task Baseline_NoBaselineExists_ReturnsZeroExpected()
    {
        var ds = await CreateDataSourceAsync("no-baseline");
        var pipeline = CreatePipeline();

        await pipeline.ProcessEntriesAsync(
        [
            new RawLogEntry
            {
                Message = "Brand new error never seen before",
                Timestamp = DateTimeOffset.UtcNow,
                Level = "error",
            }
        ], ds.Id, null);

        await using var ctx = db.CreateDbContext();
        var pattern = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds.Id);

        var calculator = new BaselineCalculator(ctx);
        var comparison = await calculator.GetCurrentComparisonAsync(pattern.Id);

        Assert.NotNull(comparison);
        Assert.Equal(1, comparison.CurrentRate);
        Assert.Equal(0.0, comparison.ExpectedRate, precision: 6);
        Assert.Equal(0.0, comparison.StdDevsFromMean, precision: 6);
    }

    [Fact]
    public async Task Baseline_NonExistentPattern_ReturnsNull()
    {
        await using var ctx = db.CreateDbContext();
        var calculator = new BaselineCalculator(ctx);
        var result = await calculator.GetCurrentComparisonAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // ── Data isolation between data sources ───────────────────────

    [Fact]
    public async Task SameMessage_DifferentDataSources_CreateSeparatePatterns()
    {
        var ds1 = await CreateDataSourceAsync("isolation-1");
        var ds2 = await CreateDataSourceAsync("isolation-2");
        var pipeline = CreatePipeline();
        var now = DateTimeOffset.UtcNow;
        const string message = "Connection timeout after 30s";

        await pipeline.ProcessEntriesAsync(
            [new RawLogEntry { Message = message, Timestamp = now, Level = "error" }],
            ds1.Id, null);

        await pipeline.ProcessEntriesAsync(
            [new RawLogEntry { Message = message, Timestamp = now, Level = "error" }],
            ds2.Id, null);

        await using var ctx = db.CreateDbContext();
        var p1 = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds1.Id);
        var p2 = await ctx.LogPatterns.SingleAsync(p => p.DataSourceId == ds2.Id);

        Assert.NotEqual(p1.Id, p2.Id);
    }
}
