using LogJammer.Core.Entities;

namespace LogJammer.Core.Interfaces;

public interface IFingerprintConfigRepository
{
    Task<IReadOnlyList<FingerprintConfig>> GetByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
    Task<FingerprintConfig?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FingerprintConfig> AddAsync(FingerprintConfig config, CancellationToken cancellationToken = default);
    Task UpdateAsync(FingerprintConfig config, CancellationToken cancellationToken = default);
    Task DeleteAsync(FingerprintConfig config, CancellationToken cancellationToken = default);
}
