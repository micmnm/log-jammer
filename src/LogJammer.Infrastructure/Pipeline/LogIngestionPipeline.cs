using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class LogIngestionPipeline(
    ISchemaMapper schemaMapper,
    IFingerprintCalculator fingerprintCalculator,
    IKnownErrorRepository knownErrorRepo,
    IErrorOccurrenceRepository occurrenceRepo,
    LogJammerDbContext dbContext,
    ILogger<LogIngestionPipeline> logger) : ILogIngestionPipeline
{
    public async Task<IngestionResult> ProcessEntriesAsync(
        DataSource dataSource,
        IReadOnlyList<RawLogEntry> entries,
        double sampleRatio,
        CancellationToken cancellationToken = default)
    {
        var fingerprintConfigs = dataSource.FingerprintConfigs.ToList();
        int accepted = 0;
        int duplicates = 0;
        int failed = 0;

        foreach (var entry in entries)
        {
            try
            {
                var mapped = schemaMapper.Map(entry, dataSource.SchemaMapping);
                var fingerprint = fingerprintCalculator.ComputeFingerprint(mapped, fingerprintConfigs);

                var knownError = await knownErrorRepo.GetByFingerprintHashAsync(fingerprint, cancellationToken);
                knownError ??= await knownErrorRepo.GetByFingerprintAliasAsync(fingerprint, cancellationToken);

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

                    dbContext.ClassificationQueue.Add(new ClassificationQueueItem
                    {
                        KnownErrorId = knownError.Id
                    });
                    await dbContext.SaveChangesAsync(cancellationToken);

                    accepted++;
                }
                else
                {
                    knownError.LastSeen = mapped.Timestamp > knownError.LastSeen ? mapped.Timestamp : knownError.LastSeen;
                    knownError.TotalOccurrences++;
                    await knownErrorRepo.UpdateAsync(knownError, cancellationToken);
                    duplicates++;
                }

                await occurrenceRepo.UpsertWindowAsync(
                    knownError.Id, mapped.Timestamp, mapped.Timestamp.AddMinutes(5),
                    sampleRatio, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to process entry for data source {DataSourceId}", dataSource.Id);
                failed++;
            }
        }

        return new IngestionResult(accepted, duplicates, failed);
    }
}
