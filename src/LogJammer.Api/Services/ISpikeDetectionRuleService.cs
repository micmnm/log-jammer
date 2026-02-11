using LogJammer.Api.Dtos;

namespace LogJammer.Api.Services;

public interface ISpikeDetectionRuleService
{
    Task<IReadOnlyList<SpikeDetectionRuleDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SpikeDetectionRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SpikeDetectionRuleDto> CreateAsync(CreateSpikeDetectionRuleRequest request, CancellationToken cancellationToken = default);
    Task<SpikeDetectionRuleDto?> UpdateAsync(Guid id, UpdateSpikeDetectionRuleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
