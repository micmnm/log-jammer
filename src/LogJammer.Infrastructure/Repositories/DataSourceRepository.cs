using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class DataSourceRepository(LogJammerDbContext context) : IDataSourceRepository
{
    public async Task<IReadOnlyList<DataSource>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.DataSources
            .AsNoTracking()
            .OrderBy(ds => ds.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DataSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.DataSources
            .Include(ds => ds.FingerprintConfigs)
            .FirstOrDefaultAsync(ds => ds.Id == id, cancellationToken);
    }

    public async Task<DataSource> AddAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        context.DataSources.Add(dataSource);
        await context.SaveChangesAsync(cancellationToken);
        return dataSource;
    }

    public async Task UpdateAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        context.DataSources.Update(dataSource);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(DataSource dataSource, CancellationToken cancellationToken = default)
    {
        context.DataSources.Remove(dataSource);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.DataSources.AnyAsync(ds => ds.Id == id, cancellationToken);
    }

    public async Task<DeletionImpact> GetDeletionImpactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var errorGroupIds = await context.KnownErrors
            .Where(e => e.DataSourceId == id)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        if (errorGroupIds.Count == 0)
            return new DeletionImpact(0, 0, 0, 0, 0, 0);

        var occurrenceCount = await context.ErrorOccurrences
            .CountAsync(o => errorGroupIds.Contains(o.KnownErrorId), cancellationToken);

        var alertCount = await context.Alerts
            .CountAsync(a => errorGroupIds.Contains(a.KnownErrorId), cancellationToken);

        var classificationQueueCount = await context.ClassificationQueue
            .CountAsync(q => errorGroupIds.Contains(q.KnownErrorId), cancellationToken);

        var tagCount = await context.ErrorTags
            .CountAsync(t => errorGroupIds.Contains(t.KnownErrorId), cancellationToken);

        var ruleCount = await context.SpikeDetectionRules
            .CountAsync(r => r.KnownErrorId != null && errorGroupIds.Contains(r.KnownErrorId.Value), cancellationToken);

        return new DeletionImpact(
            errorGroupIds.Count,
            occurrenceCount,
            alertCount,
            classificationQueueCount,
            tagCount,
            ruleCount);
    }

    public async Task DetachKnownErrorsAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        await context.KnownErrors
            .Where(e => e.DataSourceId == dataSourceId)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.DataSourceId, (Guid?)null), cancellationToken);
    }
}
