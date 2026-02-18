using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;

namespace LogJammer.Api.Services;

public interface IIngestService
{
    Task<(int Accepted, int Duplicates, int Failed)> IngestAsync(
        Guid dataSourceId,
        IReadOnlyList<(DateTime Timestamp, Dictionary<string, object?> Fields)> entries,
        CancellationToken cancellationToken = default);
}

public class IngestService(
    IDataSourceRepository dataSourceRepo,
    ILogIngestionPipeline ingestionPipeline) : IIngestService
{
    public async Task<(int Accepted, int Duplicates, int Failed)> IngestAsync(
        Guid dataSourceId,
        IReadOnlyList<(DateTime Timestamp, Dictionary<string, object?> Fields)> entries,
        CancellationToken cancellationToken = default)
    {
        var dataSource = await dataSourceRepo.GetByIdAsync(dataSourceId, cancellationToken);
        if (dataSource is null)
            throw new KeyNotFoundException($"Data source {dataSourceId} not found");

        if (dataSource.AdapterType != AdapterType.KibanaProxy)
            throw new InvalidOperationException(
                $"Data source {dataSourceId} is not a KibanaProxy source. Only KibanaProxy sources accept pushed data.");

        var rawEntries = entries
            .Select(e => new RawLogEntry(e.Timestamp, e.Fields))
            .ToList();

        var result = await ingestionPipeline.ProcessEntriesAsync(dataSource, rawEntries, 1.0, cancellationToken);
        return (result.Accepted, result.Duplicates, result.Failed);
    }
}
