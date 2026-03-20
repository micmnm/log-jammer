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
                Template = Truncate(result.Template, 2000),
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

    public async Task<AcknowledgeResult> AcknowledgeAsync(Guid patternId)
    {
        var pattern = await db.LogPatterns.FirstOrDefaultAsync(p => p.Id == patternId);
        if (pattern is null)
            return new AcknowledgeResult(0, []);

        pattern.IsNew = false;

        // Find similar "new" patterns within the same data source
        var newPatterns = await db.LogPatterns
            .Where(p => p.IsNew && p.Id != patternId && p.DataSourceId == pattern.DataSourceId)
            .ToListAsync();

        var alsoAcknowledged = new List<SimilarPatternMatch>();
        foreach (var candidate in newPatterns)
        {
            var similarity = DrainParser.ComputeTemplateSimilarity(pattern.Template, candidate.Template);
            if (similarity >= SimilarityThreshold)
            {
                candidate.IsNew = false;
                alsoAcknowledged.Add(new SimilarPatternMatch(candidate.Id, candidate.Template, similarity));
            }
        }

        await db.SaveChangesAsync();
        return new AcknowledgeResult(alsoAcknowledged.Count, alsoAcknowledged);
    }

    private const double SimilarityThreshold = 0.6;

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

public record AcknowledgeResult(int SimilarCount, List<SimilarPatternMatch> SimilarPatterns);
public record SimilarPatternMatch(Guid Id, string Template, double Similarity);
