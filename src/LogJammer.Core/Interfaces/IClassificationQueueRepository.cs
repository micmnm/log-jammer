using LogJammer.Core.Entities;

namespace LogJammer.Core.Interfaces;

public interface IClassificationQueueRepository
{
    Task<IReadOnlyList<ClassificationQueueItem>> GetPendingAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);
    Task<ClassificationQueueItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(ClassificationQueueItem item, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassificationQueueItem>> GetUnprocessedAsync(int batchSize, CancellationToken cancellationToken = default);
}
