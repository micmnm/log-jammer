using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class CorrelatedSpikeAlertRepository(LogJammerDbContext context) : ICorrelatedSpikeAlertRepository
{
    public async Task<IReadOnlyList<CorrelatedSpikeAlert>> GetAllAsync(AlertStatus? status = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var query = context.CorrelatedSpikeAlerts
            .Include(c => c.DataSource)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        return await query
            .OrderByDescending(c => c.DetectedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<CorrelatedSpikeAlert?> GetActiveByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        return await context.CorrelatedSpikeAlerts
            .FirstOrDefaultAsync(c => c.DataSourceId == dataSourceId
                && c.Status != AlertStatus.Resolved, cancellationToken);
    }

    public async Task<CorrelatedSpikeAlert> AddAsync(CorrelatedSpikeAlert alert, CancellationToken cancellationToken = default)
    {
        context.CorrelatedSpikeAlerts.Add(alert);
        await context.SaveChangesAsync(cancellationToken);
        return alert;
    }

    public async Task UpdateAsync(CorrelatedSpikeAlert alert, CancellationToken cancellationToken = default)
    {
        context.CorrelatedSpikeAlerts.Update(alert);
        await context.SaveChangesAsync(cancellationToken);
    }
}
