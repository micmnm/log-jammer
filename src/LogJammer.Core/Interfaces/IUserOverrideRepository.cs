using LogJammer.Core.Entities;

namespace LogJammer.Core.Interfaces;

public interface IUserOverrideRepository
{
    Task<UserOverride> AddAsync(UserOverride userOverride, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserOverride>> GetByKnownErrorAsync(Guid knownErrorId, CancellationToken cancellationToken = default);
    Task<UserOverride?> GetByKnownErrorAndTypeAsync(Guid knownErrorId, string overrideType, CancellationToken cancellationToken = default);
}
