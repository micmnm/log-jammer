using LogJammer.Core.Entities;
using LogJammer.Core.Models;

namespace LogJammer.Core.Interfaces;

public record IngestionResult(int Accepted, int Duplicates, int Failed);

public interface ILogIngestionPipeline
{
    Task<IngestionResult> ProcessEntriesAsync(
        DataSource dataSource,
        IReadOnlyList<RawLogEntry> entries,
        double sampleRatio,
        CancellationToken cancellationToken = default);
}
