using LogJammer.Core.Entities;
using LogJammer.Core.Models;

namespace LogJammer.Core.Interfaces;

public interface IDataSourceRepository
{
    Task<IReadOnlyList<DataSource>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DataSource?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DataSource> AddAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task UpdateAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task DeleteAsync(DataSource dataSource, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DeletionImpact> GetDeletionImpactAsync(Guid id, CancellationToken cancellationToken = default);
    Task DetachKnownErrorsAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
}
