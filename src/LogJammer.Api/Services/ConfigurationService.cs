using LogJammer.Api.Dtos;
using LogJammer.Core.Interfaces;

namespace LogJammer.Api.Services;

public class ConfigurationService(IClassificationConfigRepository configRepo) : IConfigurationService
{
    public async Task<IReadOnlyList<ConfigurationResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var configs = await configRepo.GetAllAsync(cancellationToken);
        return configs.Select(c => new ConfigurationResponse
        {
            Key = c.Key,
            Value = c.Value,
            Description = c.Description,
            UpdatedAt = c.UpdatedAt
        }).ToList();
    }

    public async Task<ConfigurationResponse> UpdateAsync(UpdateConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        var config = await configRepo.UpsertAsync(request.Key, request.Value, cancellationToken: cancellationToken);
        return new ConfigurationResponse
        {
            Key = config.Key,
            Value = config.Value,
            Description = config.Description,
            UpdatedAt = config.UpdatedAt
        };
    }
}
