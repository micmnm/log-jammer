using LogJammer.Api.Dtos;
using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;

namespace LogJammer.Api.Services;

public class FingerprintConfigService(IFingerprintConfigRepository repository) : IFingerprintConfigService
{
    public async Task<IReadOnlyList<FingerprintConfigResponse>> GetByDataSourceIdAsync(Guid dataSourceId, CancellationToken cancellationToken = default)
    {
        var configs = await repository.GetByDataSourceIdAsync(dataSourceId, cancellationToken);
        return configs.Select(MapToResponse).ToList();
    }

    public async Task<FingerprintConfigResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await repository.GetByIdAsync(id, cancellationToken);
        return config is null ? null : MapToResponse(config);
    }

    public async Task<FingerprintConfigResponse> CreateAsync(Guid dataSourceId, CreateFingerprintConfigRequest request, CancellationToken cancellationToken = default)
    {
        var config = new FingerprintConfig
        {
            DataSourceId = dataSourceId,
            FieldName = request.FieldName,
            Order = request.Order,
            NormalizeBeforeHash = request.NormalizeBeforeHash
        };

        config = await repository.AddAsync(config, cancellationToken);
        return MapToResponse(config);
    }

    public async Task<FingerprintConfigResponse?> UpdateAsync(Guid id, UpdateFingerprintConfigRequest request, CancellationToken cancellationToken = default)
    {
        var config = await repository.GetByIdAsync(id, cancellationToken);
        if (config is null) return null;

        if (request.FieldName is not null) config.FieldName = request.FieldName;
        if (request.Order is not null) config.Order = request.Order.Value;
        if (request.NormalizeBeforeHash is not null) config.NormalizeBeforeHash = request.NormalizeBeforeHash.Value;

        await repository.UpdateAsync(config, cancellationToken);
        return MapToResponse(config);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await repository.GetByIdAsync(id, cancellationToken);
        if (config is null) return false;

        await repository.DeleteAsync(config, cancellationToken);
        return true;
    }

    private static FingerprintConfigResponse MapToResponse(FingerprintConfig config) => new()
    {
        Id = config.Id,
        DataSourceId = config.DataSourceId,
        FieldName = config.FieldName,
        Order = config.Order,
        NormalizeBeforeHash = config.NormalizeBeforeHash,
        CreatedAt = config.CreatedAt
    };
}
