using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class KnownErrorRepository(LogJammerDbContext context) : IKnownErrorRepository
{
    public async Task<KnownError?> GetByFingerprintHashAsync(string fingerprintHash, CancellationToken cancellationToken = default)
    {
        return await context.KnownErrors
            .FirstOrDefaultAsync(e => e.FingerprintHash == fingerprintHash, cancellationToken);
    }

    public async Task<IReadOnlyList<KnownError>> GetAllAsync(
        Guid? dataSourceId = null,
        ErrorStatus? status = null,
        ErrorSeverity? severity = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = BuildFilterQuery(dataSourceId, status, severity);

        return await query
            .Include(e => e.DataSource)
            .OrderByDescending(e => e.LastSeen)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(
        Guid? dataSourceId = null,
        ErrorStatus? status = null,
        ErrorSeverity? severity = null,
        CancellationToken cancellationToken = default)
    {
        return await BuildFilterQuery(dataSourceId, status, severity)
            .CountAsync(cancellationToken);
    }

    public async Task<KnownError?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.KnownErrors
            .Include(e => e.DataSource)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<KnownError> AddAsync(KnownError knownError, CancellationToken cancellationToken = default)
    {
        context.KnownErrors.Add(knownError);
        await context.SaveChangesAsync(cancellationToken);
        return knownError;
    }

    public async Task UpdateAsync(KnownError knownError, CancellationToken cancellationToken = default)
    {
        context.KnownErrors.Update(knownError);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<KnownError?> GetByFingerprintAliasAsync(string fingerprintHash, CancellationToken cancellationToken = default)
    {
        var alias = await context.FingerprintAliases
            .Include(a => a.KnownError)
            .FirstOrDefaultAsync(a => a.FingerprintHash == fingerprintHash, cancellationToken);

        return alias?.KnownError;
    }

    public async Task MergeIntoAsync(Guid sourceKnownErrorId, Guid targetKnownErrorId, CancellationToken cancellationToken = default)
    {
        var source = await context.KnownErrors
            .Include(e => e.Occurrences)
            .FirstOrDefaultAsync(e => e.Id == sourceKnownErrorId, cancellationToken);

        // Idempotent: no-op if source already deleted
        if (source is null) return;

        var target = await context.KnownErrors
            .Include(e => e.Occurrences)
            .FirstAsync(e => e.Id == targetKnownErrorId, cancellationToken);

        // Move occurrences: merge counts for overlapping windows, re-parent others
        foreach (var sourceOcc in source.Occurrences)
        {
            var overlapping = target.Occurrences.FirstOrDefault(t =>
                t.WindowStart == sourceOcc.WindowStart && t.WindowEnd == sourceOcc.WindowEnd);

            if (overlapping is not null)
            {
                overlapping.Count += sourceOcc.Count;
                context.ErrorOccurrences.Remove(sourceOcc);
            }
            else
            {
                sourceOcc.KnownErrorId = targetKnownErrorId;
            }
        }

        // Update target aggregate fields
        target.TotalOccurrences += source.TotalOccurrences;
        if (source.FirstSeen < target.FirstSeen)
            target.FirstSeen = source.FirstSeen;
        if (source.LastSeen > target.LastSeen)
            target.LastSeen = source.LastSeen;

        // Create alias so future lookups route directly to target
        context.FingerprintAliases.Add(new FingerprintAlias
        {
            FingerprintHash = source.FingerprintHash,
            KnownErrorId = targetKnownErrorId
        });

        // Delete source (cascades: ErrorTags, Alerts, ClassificationQueueItem, UserOverrides)
        context.KnownErrors.Remove(source);

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<(KnownError? Match, double Similarity)> FindNearestByEmbeddingAsync(
        float[] embedding, double threshold, CancellationToken cancellationToken = default)
    {
        var vector = new Pgvector.Vector(embedding);

        var nearest = await context.KnownErrors
            .Where(e => e.EmbeddingVector != null)
            .OrderBy(e => e.EmbeddingVector!.CosineDistance(vector))
            .Select(e => new { Error = e, Distance = e.EmbeddingVector!.CosineDistance(vector) })
            .Take(1)
            .FirstOrDefaultAsync(cancellationToken);

        if (nearest is null)
            return (null, 0);

        var similarity = 1.0 - nearest.Distance;
        if (similarity < threshold)
            return (null, similarity);

        return (nearest.Error, similarity);
    }

    private IQueryable<KnownError> BuildFilterQuery(Guid? dataSourceId, ErrorStatus? status, ErrorSeverity? severity)
    {
        var query = context.KnownErrors.AsQueryable();

        if (dataSourceId.HasValue)
            query = query.Where(e => e.DataSourceId == dataSourceId.Value);
        if (status.HasValue)
            query = query.Where(e => e.Status == status.Value);
        if (severity.HasValue)
            query = query.Where(e => e.Severity == severity.Value);

        return query;
    }
}
