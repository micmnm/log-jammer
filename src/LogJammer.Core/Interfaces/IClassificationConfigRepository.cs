using LogJammer.Core.Entities;

namespace LogJammer.Core.Interfaces;

public interface IClassificationConfigRepository
{
    Task<ClassificationConfig?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClassificationConfig>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ClassificationConfig> UpsertAsync(string key, string value, string? description = null, CancellationToken cancellationToken = default);
}
