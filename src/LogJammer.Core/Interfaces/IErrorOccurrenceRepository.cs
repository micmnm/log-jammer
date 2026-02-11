using LogJammer.Core.Entities;

namespace LogJammer.Core.Interfaces;

public interface IErrorOccurrenceRepository
{
    Task UpsertWindowAsync(Guid knownErrorId, DateTime windowStart, DateTime windowEnd, double? sampleRatio, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ErrorOccurrence>> GetByKnownErrorAsync(Guid knownErrorId, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);
    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
