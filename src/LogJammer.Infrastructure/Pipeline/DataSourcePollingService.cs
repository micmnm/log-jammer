using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class DataSourcePollingService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Guid _dataSourceId;
    private readonly ILogger _logger;
    private DateTime _lastPollTime = DateTime.UtcNow.AddHours(-1);

    public DataSourcePollingService(IServiceScopeFactory scopeFactory, Guid dataSourceId, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _dataSourceId = dataSourceId;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Polling service started for data source {DataSourceId}", _dataSourceId);

        while (!cancellationToken.IsCancellationRequested)
        {
            int pollInterval = 30;
            try
            {
                pollInterval = await ExecutePollCycleAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in poll cycle for data source {DataSourceId}", _dataSourceId);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(pollInterval), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Polling service stopped for data source {DataSourceId}", _dataSourceId);
    }

    private async Task<int> ExecutePollCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dataSourceRepo = scope.ServiceProvider.GetRequiredService<IDataSourceRepository>();
        var adapterFactory = scope.ServiceProvider.GetRequiredService<IDataSourceAdapterFactory>();
        var schemaMapper = scope.ServiceProvider.GetRequiredService<ISchemaMapper>();
        var fingerprintCalculator = scope.ServiceProvider.GetRequiredService<IFingerprintCalculator>();
        var knownErrorRepo = scope.ServiceProvider.GetRequiredService<IKnownErrorRepository>();
        var occurrenceRepo = scope.ServiceProvider.GetRequiredService<IErrorOccurrenceRepository>();

        var dataSource = await dataSourceRepo.GetByIdAsync(_dataSourceId, cancellationToken);
        if (dataSource is null || !dataSource.Enabled)
        {
            _logger.LogWarning("Data source {DataSourceId} not found or disabled, skipping poll", _dataSourceId);
            return 30;
        }

        var adapter = adapterFactory.CreateAdapter(dataSource.AdapterType, dataSource.ConnectionConfig);
        var batch = await adapter.PollErrorsAsync(_lastPollTime, dataSource.SamplingBudget, cancellationToken);

        if (batch.Entries.Count == 0)
        {
            _logger.LogDebug("No new entries for data source {DataSourceId}", _dataSourceId);
            _lastPollTime = DateTime.UtcNow;
            return dataSource.PollIntervalSeconds;
        }

        _logger.LogInformation("Processing {Count} entries for data source {DataSourceId}", batch.Entries.Count, _dataSourceId);

        var fingerprintConfigs = dataSource.FingerprintConfigs.ToList();

        foreach (var entry in batch.Entries)
        {
            try
            {
                var mapped = schemaMapper.Map(entry, dataSource.SchemaMapping);
                var fingerprint = fingerprintCalculator.ComputeFingerprint(mapped, fingerprintConfigs);

                var knownError = await knownErrorRepo.GetByFingerprintHashAsync(fingerprint, cancellationToken);

                if (knownError is null)
                {
                    knownError = await knownErrorRepo.AddAsync(new KnownError
                    {
                        FingerprintHash = fingerprint,
                        RepresentativeMessage = mapped.Message,
                        RepresentativeStackTrace = mapped.StackTrace,
                        Severity = mapped.Severity ?? ErrorSeverity.Warning,
                        Status = ErrorStatus.Active,
                        FirstSeen = mapped.Timestamp,
                        LastSeen = mapped.Timestamp,
                        TotalOccurrences = 1,
                        DataSourceId = dataSource.Id
                    }, cancellationToken);

                    // Queue for classification
                    var dbContext = scope.ServiceProvider.GetRequiredService<LogJammer.Infrastructure.Data.LogJammerDbContext>();
                    dbContext.ClassificationQueue.Add(new ClassificationQueueItem
                    {
                        KnownErrorId = knownError.Id
                    });
                    await dbContext.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("New error fingerprint {Fingerprint} for data source {DataSourceId}", fingerprint[..12], _dataSourceId);
                }
                else
                {
                    knownError.LastSeen = mapped.Timestamp > knownError.LastSeen ? mapped.Timestamp : knownError.LastSeen;
                    knownError.TotalOccurrences++;
                    await knownErrorRepo.UpdateAsync(knownError, cancellationToken);
                }

                await occurrenceRepo.UpsertWindowAsync(knownError.Id, mapped.Timestamp, mapped.Timestamp.AddMinutes(5), batch.SampleRatio, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process entry for data source {DataSourceId}", _dataSourceId);
            }
        }

        _lastPollTime = DateTime.UtcNow;
        return dataSource.PollIntervalSeconds;
    }
}
