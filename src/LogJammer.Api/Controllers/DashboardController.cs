using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController(LogJammerDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get()
    {
        var now = DateTimeOffset.UtcNow;
        var currentWindowStart = new DateTimeOffset(
            now.UtcDateTime.Year, now.UtcDateTime.Month, now.UtcDateTime.Day,
            now.UtcDateTime.Hour, 0, 0, TimeSpan.Zero);
        var currentHourOfWeek = (int)now.UtcDateTime.DayOfWeek * 24 + now.UtcDateTime.Hour;

        // Total and new pattern counts
        var totalPatterns = await db.LogPatterns.CountAsync();
        var newPatternCount = await db.LogPatterns.CountAsync(p => p.IsNew);

        // Ingestion rate = sum of all occurrences in the current hour window
        var ingestionRatePerHour = await db.PatternOccurrences
            .Where(o => o.WindowStart == currentWindowStart)
            .SumAsync(o => (long?)o.Count) ?? 0L;

        // Load patterns that have occurrences in the current hour (limit to anomaly candidates)
        // Join patterns -> occurrences (current window) -> baselines (current hour of week)
        var patternsWithCurrentOccurrences = await db.LogPatterns
            .AsNoTracking()
            .Include(p => p.DataSource)
            .Where(p => p.Occurrences.Any(o => o.WindowStart == currentWindowStart))
            .Select(p => new
            {
                Pattern = p,
                CurrentCount = p.Occurrences
                    .Where(o => o.WindowStart == currentWindowStart)
                    .Sum(o => o.Count),
                Baseline = p.Baselines
                    .FirstOrDefault(b => b.HourOfWeek == currentHourOfWeek)
            })
            .ToListAsync();

        // Compute std-devs from mean for anomaly ranking
        var anomalyItems = patternsWithCurrentOccurrences
            .Select(x =>
            {
                var stdDevs = x.Baseline is not null && x.Baseline.StdDevCount > 0
                    ? (x.CurrentCount - x.Baseline.AvgCount) / x.Baseline.StdDevCount
                    : 0.0;
                return new AnomalyItem(
                    x.Pattern.Id,
                    x.Pattern.Template,
                    x.Pattern.Severity,
                    x.CurrentCount,
                    x.Baseline?.AvgCount ?? 0,
                    stdDevs,
                    x.Pattern.DataSource.Name);
            })
            .Where(a => Math.Abs(a.StdDevsFromMean) > 1.0)
            .OrderByDescending(a => Math.Abs(a.StdDevsFromMean))
            .Take(10)
            .ToList();

        // New patterns (up to 50), most recent first
        var newPatterns = await db.LogPatterns
            .AsNoTracking()
            .Include(p => p.DataSource)
            .Where(p => p.IsNew)
            .OrderByDescending(p => p.FirstSeen)
            .Take(50)
            .Select(p => new NewPatternItem(p.Id, p.Template, p.Severity, p.FirstSeen, p.DataSource.Name))
            .ToListAsync();

        return Ok(new DashboardResponse(
            totalPatterns,
            newPatternCount,
            ingestionRatePerHour,
            anomalyItems,
            newPatterns));
    }
}
