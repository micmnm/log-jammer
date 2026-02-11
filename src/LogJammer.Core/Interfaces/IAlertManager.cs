using LogJammer.Core.Models;

namespace LogJammer.Core.Interfaces;

public interface IAlertManager
{
    Task ProcessSpikeResultAsync(SpikeResult result, Guid dataSourceId, CancellationToken cancellationToken = default);
    Task AcknowledgeAsync(Guid alertId, CancellationToken cancellationToken = default);
    Task ResolveAsync(Guid alertId, CancellationToken cancellationToken = default);
}
