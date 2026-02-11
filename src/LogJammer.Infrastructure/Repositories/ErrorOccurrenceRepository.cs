using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class ErrorOccurrenceRepository(LogJammerDbContext context) : IErrorOccurrenceRepository
{
    public async Task UpsertWindowAsync(Guid knownErrorId, DateTime windowStart, DateTime windowEnd, double? sampleRatio, CancellationToken cancellationToken = default)
    {
        // Floor to 5-minute window
        var floored = FloorTo5Minutes(windowStart);
        var flooredEnd = floored.AddMinutes(5);

        var existing = await context.ErrorOccurrences
            .FirstOrDefaultAsync(o => o.KnownErrorId == knownErrorId && o.WindowStart == floored, cancellationToken);

        if (existing is not null)
        {
            existing.Count++;
            existing.SampleRatio = sampleRatio ?? existing.SampleRatio;
        }
        else
        {
            context.ErrorOccurrences.Add(new ErrorOccurrence
            {
                KnownErrorId = knownErrorId,
                WindowStart = floored,
                WindowEnd = flooredEnd,
                Count = 1,
                SampleRatio = sampleRatio
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ErrorOccurrence>> GetByKnownErrorAsync(Guid knownErrorId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        var query = context.ErrorOccurrences
            .Where(o => o.KnownErrorId == knownErrorId);

        if (from.HasValue)
            query = query.Where(o => o.WindowStart >= from.Value);
        if (to.HasValue)
            query = query.Where(o => o.WindowEnd <= to.Value);

        return await query
            .OrderBy(o => o.WindowStart)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        return await context.ErrorOccurrences
            .Where(o => o.WindowEnd < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static DateTime FloorTo5Minutes(DateTime dt)
    {
        var minute = dt.Minute;
        var floored = minute - (minute % 5);
        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, floored, 0, DateTimeKind.Utc);
    }
}
