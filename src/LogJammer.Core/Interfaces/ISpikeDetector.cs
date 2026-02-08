using LogJammer.Core.Models;

namespace LogJammer.Core.Interfaces;

public interface ISpikeDetector
{
    Task<SpikeResult?> EvaluateAsync(Guid knownErrorId, CancellationToken cancellationToken = default);
}
