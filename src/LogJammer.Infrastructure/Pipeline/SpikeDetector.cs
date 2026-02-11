using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;
using LogJammer.Core.Models;
using Microsoft.Extensions.Logging;

namespace LogJammer.Infrastructure.Pipeline;

public class SpikeDetector(
    IErrorOccurrenceRepository occurrenceRepo,
    ISpikeDetectionRuleRepository ruleRepo,
    ILogger<SpikeDetector> logger) : ISpikeDetector
{
    public async Task<SpikeResult?> EvaluateAsync(Guid knownErrorId, CancellationToken cancellationToken = default)
    {
        var rule = await ruleRepo.GetByKnownErrorIdAsync(knownErrorId, cancellationToken);
        if (rule is null || !rule.Enabled)
            return null;

        var now = DateTime.UtcNow;
        var windowStart = now.AddMinutes(-rule.WindowMinutes);

        var occurrences = await occurrenceRepo.GetByKnownErrorAsync(knownErrorId, windowStart, cancellationToken: cancellationToken);
        var currentSum = occurrences.Sum(o => o.Count);

        return rule.ThresholdType switch
        {
            ThresholdType.Absolute => EvaluateAbsolute(knownErrorId, rule.ThresholdValue, currentSum),
            ThresholdType.PercentageIncrease => await EvaluatePercentage(knownErrorId, rule, currentSum, now, cancellationToken),
            _ => null
        };
    }

    private static SpikeResult EvaluateAbsolute(Guid knownErrorId, double threshold, long currentSum)
    {
        return new SpikeResult(
            knownErrorId,
            ThresholdType.Absolute,
            threshold,
            currentSum,
            currentSum >= threshold);
    }

    private async Task<SpikeResult?> EvaluatePercentage(Guid knownErrorId, Core.Entities.SpikeDetectionRule rule, long currentSum, DateTime now, CancellationToken cancellationToken)
    {
        var lookbackStart = now.AddMinutes(-rule.LookbackMinutes);
        var windowStart = now.AddMinutes(-rule.WindowMinutes);

        var historicalOccurrences = await occurrenceRepo.GetByKnownErrorAsync(knownErrorId, lookbackStart, windowStart, cancellationToken);

        if (historicalOccurrences.Count == 0)
        {
            logger.LogDebug("No historical data for {KnownErrorId}, skipping percentage evaluation", knownErrorId);
            return null;
        }

        var totalHistorical = historicalOccurrences.Sum(o => o.Count);
        var windowCount = (int)Math.Floor((double)(rule.LookbackMinutes - rule.WindowMinutes) / rule.WindowMinutes);
        if (windowCount <= 0) return null;

        var baseline = (double)totalHistorical / windowCount;

        if (baseline <= 0)
        {
            logger.LogDebug("Baseline is zero for {KnownErrorId}, skipping percentage evaluation", knownErrorId);
            return null;
        }

        var percentageIncrease = ((currentSum - baseline) / baseline) * 100;

        return new SpikeResult(
            knownErrorId,
            ThresholdType.PercentageIncrease,
            rule.ThresholdValue,
            percentageIncrease,
            percentageIncrease >= rule.ThresholdValue);
    }
}
