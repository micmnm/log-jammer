namespace LogJammer.Core.Interfaces;

public interface ICorrelationDetector
{
    Task DetectAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
}
