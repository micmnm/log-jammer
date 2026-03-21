using LogJammer.Engine;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LogJammer.Tests;

[Collection("Database")]
public class BaselineCalculatorTests(DatabaseFixture db)
{
    [Fact]
    public async Task RecalculateBaselines_ComputesAvgAndStdDev()
    {
        // Arrange: create a unique DataSource + LogPattern
        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"baseline-test-{Guid.NewGuid()}",
            Type = DataSourceType.KibanaProxy,
            ConnectionConfig = "{}"
        };

        var pattern = new LogPattern
        {
            Id = Guid.NewGuid(),
            Template = "Error connecting to * database",
            ClusterId = 1,
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
            SampleMessage = "Error connecting to primary database",
            Severity = Severity.Error,
            DataSourceId = dataSource.Id
        };

        // Current UTC hour-of-week
        var now = DateTimeOffset.UtcNow;
        var windowHour = new DateTimeOffset(
            now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day,
            now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);

        // 4 occurrences: one per week going back, same hour-of-week, counts 10/15/20/25.
        // Use offsets of -6, -13, -20, -27 days: each pair differs by 7 days (same day-of-week),
        // and all are safely within the 4-week (28-day) cutoff window.
        var offsets = new[] { -6, -13, -20, -27 };
        var counts = new long[] { 10, 15, 20, 25 };
        var occurrences = Enumerable.Range(0, 4).Select(i => new PatternOccurrence
        {
            Id = Guid.NewGuid(),
            PatternId = pattern.Id,
            WindowStart = windowHour.AddDays(offsets[i]),
            WindowEnd = windowHour.AddDays(offsets[i]).AddHours(1),
            Count = counts[i]
        }).ToList();

        await using (var ctx = db.CreateDbContext())
        {
            ctx.DataSources.Add(dataSource);
            ctx.LogPatterns.Add(pattern);
            ctx.PatternOccurrences.AddRange(occurrences);
            await ctx.SaveChangesAsync();
        }

        // Act
        await using (var ctx = db.CreateDbContext())
        {
            var calculator = new BaselineCalculator(ctx);
            await calculator.RecalculateBaselinesAsync(pattern.Id);
        }

        // Assert
        await using var readCtx = db.CreateDbContext();
        var baseline = await readCtx.PatternBaselines
            .FirstOrDefaultAsync(b => b.PatternId == pattern.Id);

        Assert.NotNull(baseline);
        Assert.Equal(17.5, baseline.AvgCount, precision: 6);
        Assert.True(baseline.StdDevCount > 0, $"StdDevCount should be > 0, was {baseline.StdDevCount}");
    }

    [Fact]
    public async Task GetCurrentComparison_ReturnsDeviation()
    {
        // Arrange: create unique DataSource + LogPattern
        var dataSource = new DataSource
        {
            Id = Guid.NewGuid(),
            Name = $"comparison-test-{Guid.NewGuid()}",
            Type = DataSourceType.Elasticsearch,
            ConnectionConfig = "{}"
        };

        var pattern = new LogPattern
        {
            Id = Guid.NewGuid(),
            Template = "Timeout waiting for * response",
            ClusterId = 2,
            FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
            SampleMessage = "Timeout waiting for API response",
            Severity = Severity.Warning,
            DataSourceId = dataSource.Id
        };

        var now = DateTimeOffset.UtcNow;
        var currentHourOfWeek = (int)now.UtcDateTime.DayOfWeek * 24 + now.UtcDateTime.Hour;

        var windowStart = new DateTimeOffset(
            now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day,
            now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);

        var baseline = new PatternBaseline
        {
            Id = Guid.NewGuid(),
            PatternId = pattern.Id,
            HourOfWeek = currentHourOfWeek,
            AvgCount = 5.0,
            StdDevCount = 2.0
        };

        var occurrence = new PatternOccurrence
        {
            Id = Guid.NewGuid(),
            PatternId = pattern.Id,
            WindowStart = windowStart,
            WindowEnd = windowStart.AddHours(1),
            Count = 47
        };

        await using (var ctx = db.CreateDbContext())
        {
            ctx.DataSources.Add(dataSource);
            ctx.LogPatterns.Add(pattern);
            ctx.PatternBaselines.Add(baseline);
            ctx.PatternOccurrences.Add(occurrence);
            await ctx.SaveChangesAsync();
        }

        // Act
        BaselineComparison? result;
        await using (var ctx = db.CreateDbContext())
        {
            var calculator = new BaselineCalculator(ctx);
            result = await calculator.GetCurrentComparisonAsync(pattern.Id);
        }

        // Assert
        Assert.NotNull(result);
        Assert.Equal(47L, result.CurrentRate);
        Assert.Equal(5.0, result.ExpectedRate, precision: 6);
        Assert.True(result.StdDevsFromMean > 20,
            $"StdDevsFromMean should be > 20, was {result.StdDevsFromMean}");
    }
}
