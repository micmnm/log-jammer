using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class FingerprintConfigRepository(LogJammerDbContext context) : IFingerprintConfigRepository
{
    public async Task<IReadOnlyList<FingerprintConfig>> GetByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        return await context.Set<FingerprintConfig>()
            .AsNoTracking()
            .Where(fc => fc.DataSourceId == dataSourceId)
            .OrderBy(fc => fc.Order)
            .ToListAsync(cancellationToken);
    }

    public async Task<FingerprintConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<FingerprintConfig>()
            .FirstOrDefaultAsync(fc => fc.Id == id, cancellationToken);
    }

    public async Task<FingerprintConfig> AddAsync(FingerprintConfig config, CancellationToken cancellationToken = default)
    {
        context.Set<FingerprintConfig>().Add(config);
        await context.SaveChangesAsync(cancellationToken);
        return config;
    }

    public async Task UpdateAsync(FingerprintConfig config, CancellationToken cancellationToken = default)
    {
        context.Set<FingerprintConfig>().Update(config);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(FingerprintConfig config, CancellationToken cancellationToken = default)
    {
        context.Set<FingerprintConfig>().Remove(config);
        await context.SaveChangesAsync(cancellationToken);
    }
}
