using LogJammer.Core.Entities;
using LogJammer.Core.Enums;

namespace LogJammer.Core.Interfaces;

public interface IAlertRepository
{
    Task<Alert?> GetActiveByKnownErrorIdAsync(Guid knownErrorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetAllAsync(AlertStatus? status = null, Guid? dataSourceId = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(AlertStatus? status = null, Guid? dataSourceId = null, CancellationToken cancellationToken = default);
    Task<Alert?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Alert> AddAsync(Alert alert, CancellationToken cancellationToken = default);
    Task UpdateAsync(Alert alert, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Alert>> GetRecentByDataSourceAsync(Guid dataSourceId, DateTime since, CancellationToken cancellationToken = default);
}
