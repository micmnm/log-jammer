using LogJammer.Api.Dtos;

namespace LogJammer.Api.Services;

public interface IFingerprintConfigService
{
    Task<IReadOnlyList<FingerprintConfigResponse>> GetByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
    Task<FingerprintConfigResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FingerprintConfigResponse> CreateAsync(Guid dataSourceId, CreateFingerprintConfigRequest request, CancellationToken cancellationToken = default);
    Task<FingerprintConfigResponse?> UpdateAsync(Guid id, UpdateFingerprintConfigRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
