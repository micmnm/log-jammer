using LogJammer.Core.Entities;
using LogJammer.Core.Enums;

namespace LogJammer.Core.Interfaces;

public interface IKnownErrorRepository
{
    Task<KnownError?> GetByFingerprintHashAsync(string fingerprintHash, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnownError>> GetAllAsync(
        Guid? dataSourceId = null,
        ErrorStatus? status = null,
        ErrorSeverity? severity = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
    Task<int> GetCountAsync(
        Guid? dataSourceId = null,
        ErrorStatus? status = null,
        ErrorSeverity? severity = null,
        CancellationToken cancellationToken = default);
    Task<KnownError?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<KnownError> AddAsync(KnownError knownError, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnownError knownError, CancellationToken cancellationToken = default);
}
