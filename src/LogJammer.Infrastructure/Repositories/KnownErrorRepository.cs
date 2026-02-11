using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
