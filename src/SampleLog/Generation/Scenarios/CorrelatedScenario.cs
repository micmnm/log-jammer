using SampleLog.Models;

namespace SampleLog.Generation.Scenarios;

public sealed class CorrelatedScenario(LogGenerator generator, CorrelationGroup group, int burstCount, int durationSeconds) : IScenario
{
    private readonly Random _random = new();

    public string Name => $"Correlated [{group.Name}]";
    public string Description => $"{burstCount} bursts of {group.TemplateIds.Count} errors over {durationSeconds}s";

    public async Task RunAsync(CancellationToken ct)
    {
        var intervalBetweenBursts = TimeSpan.FromMilliseconds(durationSeconds * 1000.0 / burstCount);

        for (int burst = 0; burst < burstCount && !ct.IsCancellationRequested; burst++)
        {
            // Fire all templates in the group with small jitter
            foreach (var templateId in group.TemplateIds)
            {
                generator.EmitTemplate(templateId);
                var jitter = TimeSpan.FromMilliseconds(_random.Next(50, 200));
                try { await Task.Delay(jitter, ct); }
                catch (OperationCanceledException) { return; }
            }

            // Wait between bursts
            try { await Task.Delay(intervalBetweenBursts, ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
