using LogJammer.Core.Entities;
using LogJammer.Core.Enums;

namespace LogJammer.Core.Interfaces;

public interface ICorrelatedSpikeAlertRepository
{
    Task<IReadOnlyList<CorrelatedSpikeAlert>> GetAllAsync(AlertStatus? status = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<CorrelatedSpikeAlert?> GetActiveByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
    Task<CorrelatedSpikeAlert> AddAsync(CorrelatedSpikeAlert alert, CancellationToken cancellationToken = default);
    Task UpdateAsync(CorrelatedSpikeAlert alert, CancellationToken cancellationToken = default);
}
