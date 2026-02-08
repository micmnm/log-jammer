using LogJammer.Core.Models;

namespace LogJammer.Core.Interfaces;

public interface IDataSourceAdapter
{
    Task<ConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken = default);
    Task<ErrorBatch> PollErrorsAsync(DateTime since, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RawLogEntry>> GetSampleRecordsAsync(int count, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FieldDefinition>> GetSchemaAsync(CancellationToken cancellationToken = default);
}
