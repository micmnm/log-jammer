using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
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
}
