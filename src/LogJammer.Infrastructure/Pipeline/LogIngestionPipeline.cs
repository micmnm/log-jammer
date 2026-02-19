using System.Globalization;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class LogIngestionPipeline(
    ISchemaMapper schemaMapper,
    IFingerprintCalculator fingerprintCalculator,
    IKnownErrorRepository knownErrorRepo,
    IErrorOccurrenceRepository occurrenceRepo,
    LogJammerDbContext dbContext,
    IEmbeddingProvider embeddingProvider,
    IClassificationConfigRepository configRepo,
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

        // Cache embedding config for the batch to avoid per-entry DB reads
        var (embeddingEnabled, similarityThreshold) = await LoadEmbeddingConfigAsync(cancellationToken);

        foreach (var entry in entries)
        {
            try
            {
                var mapped = schemaMapper.Map(entry, dataSource.SchemaMapping);
                var fingerprint = fingerprintCalculator.ComputeFingerprint(mapped, fingerprintConfigs);

                var knownError = await knownErrorRepo.GetByFingerprintHashAsync(fingerprint, cancellationToken);
                knownError ??= await knownErrorRepo.GetByFingerprintAliasAsync(fingerprint, cancellationToken);

                var matchedByEmbedding = false;
                Pgvector.Vector? computedEmbedding = null;

                // Embedding-based similarity fallback (uses batch-cached config)
                if (knownError is null && embeddingEnabled)
                {
                    (knownError, computedEmbedding) = await TryFindByEmbeddingSimilarityAsync(mapped, similarityThreshold, cancellationToken);
                    matchedByEmbedding = knownError is not null;
                }

                if (knownError is null)
                {
                    // Brand new error
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
                        DataSourceId = dataSource.Id,
                        EmbeddingVector = computedEmbedding
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
                    // Existing error — increment
                    knownError.LastSeen = mapped.Timestamp > knownError.LastSeen ? mapped.Timestamp : knownError.LastSeen;
                    knownError.TotalOccurrences++;
                    await knownErrorRepo.UpdateAsync(knownError, cancellationToken);

                    // Create alias if matched by embedding (so future lookups are fast)
                    if (matchedByEmbedding)
                    {
                        var aliasExists = await dbContext.FingerprintAliases
                            .AnyAsync(a => a.FingerprintHash == fingerprint, cancellationToken);
                        if (!aliasExists)
                        {
                            dbContext.FingerprintAliases.Add(new FingerprintAlias
                            {
                                FingerprintHash = fingerprint,
                                KnownErrorId = knownError.Id
                            });
                            await dbContext.SaveChangesAsync(cancellationToken);
                        }
                    }

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

        if (accepted + duplicates > 0)
        {
            dataSource.LastIngestAt = DateTime.UtcNow;
            dbContext.DataSources.Update(dataSource);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new IngestionResult(accepted, duplicates, failed);
    }

    private async Task<(bool enabled, double threshold)> LoadEmbeddingConfigAsync(CancellationToken ct)
    {
        var enabledConfig = await configRepo.GetAsync("IngestionSimilarityEnabled", ct);
        if (enabledConfig is null || !bool.TryParse(enabledConfig.Value, out var enabled) || !enabled)
            return (false, 0.80);

        var thresholdConfig = await configRepo.GetAsync("IngestionSimilarityThreshold", ct);
        var threshold = 0.80;
        if (thresholdConfig is not null && double.TryParse(thresholdConfig.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
            threshold = t;

        return (true, threshold);
    }

    private async Task<(KnownError? match, Pgvector.Vector? embedding)> TryFindByEmbeddingSimilarityAsync(
        MappedLogEntry mapped, double threshold, CancellationToken ct)
    {
        // Normalize text for better embedding input
        var text = FingerprintNormalizer.Normalize(mapped.Message);
        if (!string.IsNullOrWhiteSpace(mapped.StackTrace))
            text += " " + FingerprintNormalizer.Normalize(mapped.StackTrace);

        if (string.IsNullOrWhiteSpace(text))
            return (null, null);

        // Compute embedding
        var embeddingArray = await embeddingProvider.GenerateEmbeddingAsync(text, ct);
        var vector = new Pgvector.Vector(embeddingArray);

        // Search for nearest neighbor
        var (match, similarity) = await knownErrorRepo.FindNearestByEmbeddingAsync(embeddingArray, threshold, ct);

        if (match is not null)
        {
            logger.LogInformation(
                "Embedding similarity match: grouped with KnownError {TargetId} (similarity={Similarity:F3})",
                match.Id, similarity);
        }

        return (match, vector);
    }
}
