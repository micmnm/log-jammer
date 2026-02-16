using System.Text.Json;
using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class ClassificationProcessor(
    IServiceScopeFactory scopeFactory,
    ILogger<ClassificationProcessor> logger) : BackgroundService
{
    private const int PollIntervalSeconds = 10;
    private const int BatchSize = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Classification processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in classification processor");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Classification processor stopped");
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var queueRepo = scope.ServiceProvider.GetRequiredService<IClassificationQueueRepository>();
        var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
        var context = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
        var configRepo = scope.ServiceProvider.GetRequiredService<IClassificationConfigRepository>();

        var items = await queueRepo.GetUnprocessedAsync(BatchSize, ct);
        if (items.Count == 0) return;

        logger.LogDebug("Processing {Count} classification queue items", items.Count);

        var autoTagThreshold = 0.7;
        var configEntry = await configRepo.GetAsync("AutoTagConfidenceThreshold", ct);
        if (configEntry is not null && double.TryParse(configEntry.Value, out var threshold))
            autoTagThreshold = threshold;

        foreach (var item in items)
        {
            try
            {
                var result = await classificationService.ClassifyAsync(item.KnownError, ct);

                // Merge semantically duplicate error groups
                if (result.MatchedErrorGroupId.HasValue
                    && result.MatchedErrorGroupId.Value != item.KnownErrorId)
                {
                    var knownErrorRepo = scope.ServiceProvider.GetRequiredService<IKnownErrorRepository>();
                    await knownErrorRepo.MergeIntoAsync(item.KnownErrorId, result.MatchedErrorGroupId.Value, ct);
                    logger.LogInformation("Merged error {SourceId} into {TargetId} (similarity={Similarity:F3})",
                        item.KnownErrorId, result.MatchedErrorGroupId.Value, result.SimilarityScore);
                    continue; // source is deleted, skip tag assignment
                }

                item.Confidence = result.SimilarityScore;
                item.SuggestedTags = JsonSerializer.Serialize(
                    result.SuggestedTags.Select(s => new { s.TagId, s.TagName, s.Confidence }));

                if (!result.NeedsReview && result.SuggestedTags.Count > 0)
                {
                    // Auto-assign high-confidence tags
                    foreach (var suggestion in result.SuggestedTags.Where(s => s.Confidence >= autoTagThreshold))
                    {
                        var existingTag = await context.ErrorTags
                            .FirstOrDefaultAsync(et => et.KnownErrorId == item.KnownErrorId && et.TagId == suggestion.TagId, ct);

                        if (existingTag is null)
                        {
                            context.ErrorTags.Add(new ErrorTag
                            {
                                KnownErrorId = item.KnownErrorId,
                                TagId = suggestion.TagId,
                                IsAutoAssigned = true,
                                Confidence = suggestion.Confidence
                            });
                        }
                    }

                    item.Reviewed = true;
                    item.ReviewedAt = DateTime.UtcNow;
                }

                await queueRepo.UpdateAsync(item, ct);
                await context.SaveChangesAsync(ct);

                logger.LogDebug("Classified error {ErrorId}: similarity={Similarity:F3}, tags={TagCount}, needsReview={NeedsReview}",
                    item.KnownErrorId, result.SimilarityScore, result.SuggestedTags.Count, result.NeedsReview);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to classify queue item {ItemId}", item.Id);
            }
        }
    }
}
