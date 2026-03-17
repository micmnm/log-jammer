using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Engine;

public record BaselineComparison(long CurrentRate, double ExpectedRate, double StdDevsFromMean);

public class BaselineCalculator(LogJammerDbContext db)
{
    /// <summary>
    /// Aggregates PatternOccurrence data from the last 4 weeks into PatternBaseline rows
    /// (avg + sample stddev per hour-of-week slot).
    /// If patternId is null, recalculates for ALL patterns.
    /// </summary>
    public async Task RecalculateBaselinesAsync(Guid? patternId = null)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-28);

        var query = db.PatternOccurrences
            .Where(o => o.WindowStart >= cutoff);

        if (patternId.HasValue)
            query = query.Where(o => o.PatternId == patternId.Value);

        // Evaluate client-side since EF can't translate complex LINQ group-by with stddev
        var occurrences = await query
            .Select(o => new { o.PatternId, o.WindowStart, o.Count })
            .ToListAsync();

        // Group by PatternId + HourOfWeek
        var grouped = occurrences
            .GroupBy(o => new
            {
                o.PatternId,
                HourOfWeek = (int)o.WindowStart.UtcDateTime.DayOfWeek * 24 + o.WindowStart.UtcDateTime.Hour
            });

        foreach (var group in grouped)
        {
            var counts = group.Select(o => (double)o.Count).ToList();
            var avg = counts.Average();
            var stddev = SampleStdDev(counts);

            // Upsert: find existing baseline row for (PatternId, HourOfWeek)
            var existing = await db.PatternBaselines
                .FirstOrDefaultAsync(b =>
                    b.PatternId == group.Key.PatternId &&
                    b.HourOfWeek == group.Key.HourOfWeek);

            if (existing is not null)
            {
                existing.AvgCount = avg;
                existing.StdDevCount = stddev;
            }
            else
            {
                db.PatternBaselines.Add(new PatternBaseline
                {
                    Id = Guid.NewGuid(),
                    PatternId = group.Key.PatternId,
                    HourOfWeek = group.Key.HourOfWeek,
                    AvgCount = avg,
                    StdDevCount = stddev
                });
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Gets the current hour's occurrence count and compares it to the baseline for this hour-of-week.
    /// Returns null only if the pattern doesn't exist.
    /// If no baseline exists, returns (currentRate, 0, 0).
    /// StdDevsFromMean = (currentRate - avgCount) / stdDevCount (0 if stdDev is 0).
    /// </summary>
    public async Task<BaselineComparison?> GetCurrentComparisonAsync(Guid patternId)
    {
        var patternExists = await db.LogPatterns.AnyAsync(p => p.Id == patternId);
        if (!patternExists)
            return null;

        var now = DateTimeOffset.UtcNow;
        var currentHourOfWeek = (int)now.UtcDateTime.DayOfWeek * 24 + now.UtcDateTime.Hour;

        // Current window start = truncate to current UTC hour
        var windowStart = new DateTimeOffset(
            now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day,
            now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);

        // Look up occurrence count in the current window
        var occurrence = await db.PatternOccurrences
            .FirstOrDefaultAsync(o => o.PatternId == patternId && o.WindowStart == windowStart);

        var currentRate = occurrence?.Count ?? 0L;

        // Look up baseline for this hour-of-week
        var baseline = await db.PatternBaselines
            .FirstOrDefaultAsync(b => b.PatternId == patternId && b.HourOfWeek == currentHourOfWeek);

        if (baseline is null)
            return new BaselineComparison(currentRate, 0.0, 0.0);

        var stdDevsFromMean = baseline.StdDevCount == 0.0
            ? 0.0
            : (currentRate - baseline.AvgCount) / baseline.StdDevCount;

        return new BaselineComparison(currentRate, baseline.AvgCount, stdDevsFromMean);
    }

    private static double SampleStdDev(List<double> values)
    {
        if (values.Count <= 1)
            return 0.0;

        var mean = values.Average();
        var sumOfSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }
}
