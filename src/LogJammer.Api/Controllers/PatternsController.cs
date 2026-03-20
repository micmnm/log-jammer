using LogJammer.Api.Dtos;
using LogJammer.Engine;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/patterns")]
public class PatternsController(LogJammerDbContext db, BaselineCalculator baseline) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<PatternListItem>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? dataSourceId = null,
        [FromQuery] Severity? severity = null,
        [FromQuery] bool? isNew = null,
        [FromQuery] string? search = null)
    {
        var query = db.LogPatterns
            .AsNoTracking()
            .Include(p => p.DataSource)
            .AsQueryable();

        if (dataSourceId.HasValue)
            query = query.Where(p => p.DataSourceId == dataSourceId.Value);
        if (severity.HasValue)
            query = query.Where(p => p.Severity == severity.Value);
        if (isNew.HasValue)
            query = query.Where(p => p.IsNew == isNew.Value);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => EF.Functions.ILike(p.Template, $"%{search}%"));

        var totalCount = await query.CountAsync();

        var patterns = await query
            .OrderByDescending(p => p.LastSeen)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = new List<PatternListItem>(patterns.Count);
        foreach (var pattern in patterns)
        {
            var comparison = await baseline.GetCurrentComparisonAsync(pattern.Id);
            items.Add(new PatternListItem(
                pattern.Id,
                pattern.Template,
                pattern.Severity,
                pattern.FirstSeen,
                pattern.LastSeen,
                pattern.IsNew,
                comparison?.CurrentRate ?? 0,
                comparison?.ExpectedRate ?? 0,
                comparison?.StdDevsFromMean ?? 0,
                pattern.DataSource.Name));
        }

        return Ok(new PagedResult<PatternListItem>(items, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PatternDetailResponse>> GetById(Guid id)
    {
        var pattern = await db.LogPatterns
            .AsNoTracking()
            .Include(p => p.DataSource)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (pattern is null)
            return NotFound();

        var comparison = await baseline.GetCurrentComparisonAsync(id);

        var cutoff = DateTimeOffset.UtcNow.AddHours(-168);
        var occurrences = await db.PatternOccurrences
            .AsNoTracking()
            .Where(o => o.PatternId == id && o.WindowStart >= cutoff)
            .OrderBy(o => o.WindowStart)
            .Select(o => new OccurrencePoint(o.WindowStart, o.Count))
            .ToListAsync();

        var bands = await db.PatternBaselines
            .AsNoTracking()
            .Where(b => b.PatternId == id)
            .OrderBy(b => b.HourOfWeek)
            .Select(b => new BaselineBand(b.HourOfWeek, b.AvgCount, b.StdDevCount))
            .ToListAsync();

        return Ok(new PatternDetailResponse(
            pattern.Id,
            pattern.Template,
            pattern.Severity,
            pattern.FirstSeen,
            pattern.LastSeen,
            pattern.IsNew,
            comparison?.CurrentRate ?? 0,
            comparison?.ExpectedRate ?? 0,
            comparison?.StdDevsFromMean ?? 0,
            pattern.DataSource.Name,
            pattern.SampleMessage,
            occurrences,
            bands));
    }

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<ActionResult<AcknowledgeResult>> Acknowledge(Guid id)
    {
        var exists = await db.LogPatterns.AnyAsync(p => p.Id == id);
        if (!exists)
            return NotFound();

        var store = new PatternStore(db);
        var result = await store.AcknowledgeAsync(id);
        return Ok(result);
    }

    [HttpPost("acknowledge-all")]
    public async Task<ActionResult<object>> AcknowledgeAll([FromQuery] Guid? dataSourceId = null)
    {
        var store = new PatternStore(db);
        var count = await store.AcknowledgeAllAsync(dataSourceId);
        return Ok(new { acknowledged = count });
    }
}
