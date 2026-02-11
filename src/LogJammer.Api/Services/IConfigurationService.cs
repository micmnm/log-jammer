using LogJammer.Api.Dtos;

namespace LogJammer.Api.Services;

public interface IConfigurationService
{
    Task<IReadOnlyList<ConfigurationResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ConfigurationResponse> UpdateAsync(UpdateConfigurationRequest request, CancellationToken cancellationToken = default);
}
