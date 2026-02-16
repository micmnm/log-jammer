using LogJammer.Api.Dtos;

namespace LogJammer.Api.Services;

public interface IDataSourceService
{
    Task<IReadOnlyList<DataSourceResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<DataSourceResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DataSourceResponse> CreateAsync(CreateDataSourceRequest request, CancellationToken cancellationToken = default);
    Task<DataSourceResponse?> UpdateAsync(Guid id, UpdateDataSourceRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, bool preserveHistory = false, CancellationToken cancellationToken = default);
    Task<DeletionImpactResponse?> GetDeletionImpactAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ConnectionTestResponse?> TestConnectionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SchemaResponse?> GetSchemaAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SampleRecordsResponse?> GetSampleRecordsAsync(Guid id, int count, CancellationToken cancellationToken = default);
}
