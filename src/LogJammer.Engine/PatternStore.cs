using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using LogJammer.Engine.Drain;
using LogJammer.Engine.Processing;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Engine;

public class PatternStore(LogJammerDbContext db)
{
    public async Task RecordOccurrenceAsync(
        DrainResult result,
        Severity severity,
        string rawMessage,
        Guid dataSourceId,
        DateTimeOffset timestamp)
    {
        // Upsert LogPattern
        var pattern = await db.LogPatterns
            .FirstOrDefaultAsync(p => p.ClusterId == result.ClusterId && p.DataSourceId == dataSourceId);

        if (pattern is null)
        {
            pattern = new LogPattern
            {
                Id = Guid.NewGuid(),
                ClusterId = result.ClusterId,
                Template = result.Template,
                FirstSeen = timestamp,
                LastSeen = timestamp,
                SampleMessage = Truncate(rawMessage, 4000),
                Severity = severity,
                DataSourceId = dataSourceId,
                IsNew = true,
            };
            db.LogPatterns.Add(pattern);
        }
        else
        {
            pattern.LastSeen = timestamp;
            pattern.SampleMessage = Truncate(rawMessage, 4000);
            pattern.Template = Truncate(result.Template, 2000);
        }

        await db.SaveChangesAsync();

        // Upsert PatternOccurrence for current UTC-aligned 1-hour window
        var windowStart = new DateTimeOffset(
            timestamp.UtcDateTime.Year,
            timestamp.UtcDateTime.Month,
            timestamp.UtcDateTime.Day,
            timestamp.UtcDateTime.Hour,
            0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddHours(1);

        var occurrence = await db.PatternOccurrences
            .FirstOrDefaultAsync(o => o.PatternId == pattern.Id && o.WindowStart == windowStart);

        if (occurrence is null)
        {
            occurrence = new PatternOccurrence
            {
                Id = Guid.NewGuid(),
                PatternId = pattern.Id,
                WindowStart = windowStart,
                WindowEnd = windowEnd,
                Count = 1,
            };
            db.PatternOccurrences.Add(occurrence);
        }
        else
        {
            occurrence.Count++;
        }

        await db.SaveChangesAsync();
    }

    public async Task AcknowledgeAsync(Guid patternId)
    {
        await db.LogPatterns
            .Where(p => p.Id == patternId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsNew, false));
    }

    public async Task<int> AcknowledgeAllAsync(Guid? dataSourceId)
    {
        var query = db.LogPatterns.Where(p => p.IsNew);
        if (dataSourceId.HasValue)
        {
            query = query.Where(p => p.DataSourceId == dataSourceId.Value);
        }

        return await query.ExecuteUpdateAsync(s => s.SetProperty(p => p.IsNew, false));
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
