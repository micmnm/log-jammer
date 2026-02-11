using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class AlertRepository(LogJammerDbContext context) : IAlertRepository
{
    public async Task<Alert?> GetActiveByKnownErrorIdAsync(Guid knownErrorId, CancellationToken cancellationToken = default)
    {
        return await context.Alerts
            .FirstOrDefaultAsync(a => a.KnownErrorId == knownErrorId
                && a.Status != AlertStatus.Resolved, cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetAllAsync(AlertStatus? status = null, Guid? dataSourceId = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var query = context.Alerts.Include(a => a.KnownError).AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        if (dataSourceId.HasValue)
            query = query.Where(a => a.KnownError.DataSourceId == dataSourceId.Value);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCountAsync(AlertStatus? status = null, Guid? dataSourceId = null, CancellationToken cancellationToken = default)
    {
        var query = context.Alerts.Include(a => a.KnownError).AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);
        if (dataSourceId.HasValue)
            query = query.Where(a => a.KnownError.DataSourceId == dataSourceId.Value);

        return await query.CountAsync(cancellationToken);
    }

    public async Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Alerts
            .Include(a => a.KnownError)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<Alert> AddAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        context.Alerts.Add(alert);
        await context.SaveChangesAsync(cancellationToken);
        return alert;
    }

    public async Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default)
    {
        context.Alerts.Update(alert);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Alert>> GetRecentByDataSourceAsync(Guid dataSourceId, DateTime since, CancellationToken cancellationToken = default)
    {
        return await context.Alerts
            .Include(a => a.KnownError)
            .Where(a => a.KnownError.DataSourceId == dataSourceId && a.CreatedAt >= since)
            .OrderByDescending(a => a.CreatedAt)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
