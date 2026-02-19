using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Infrastructure.Repositories;

public class ClassificationQueueRepository(LogJammerDbContext context) : IClassificationQueueRepository
{
    public async Task<IReadOnlyList<ClassificationQueueItem>> GetPendingAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        return await context.ClassificationQueue
            .Where(q => !q.Reviewed)
            .Include(q => q.KnownError)
                .ThenInclude(ke => ke.DataSource)
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default)
    {
        return await context.ClassificationQueue
            .Where(q => !q.Reviewed)
            .CountAsync(cancellationToken);
    }

    public async Task<ClassificationQueueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.ClassificationQueue
            .Include(q => q.KnownError)
                .ThenInclude(ke => ke.DataSource)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);
    }

    public async Task UpdateAsync(ClassificationQueueItem item, CancellationToken cancellationToken = default)
    {
        context.ClassificationQueue.Update(item);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ClassificationQueueItem>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        return await context.ClassificationQueue
            .Where(q => !q.Reviewed && q.Confidence == null)
            .Include(q => q.KnownError)
            .OrderBy(q => q.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
