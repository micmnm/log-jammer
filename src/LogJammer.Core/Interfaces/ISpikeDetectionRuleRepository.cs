using LogJammer.Core.Entities;

namespace LogJammer.Core.Interfaces;

public interface ISpikeDetectionRuleRepository
{
    Task<SpikeDetectionRule?> GetByKnownErrorIdAsync(Guid? knownErrorId, CancellationToken cancellationToken = default);
    Task<SpikeDetectionRule?> GetGlobalDefaultAsync(CancellationToken cancellationToken = default);
    Task<SpikeDetectionRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SpikeDetectionRule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<SpikeDetectionRule> AddAsync(SpikeDetectionRule rule, CancellationToken cancellationToken = default);
    Task UpdateAsync(SpikeDetectionRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
