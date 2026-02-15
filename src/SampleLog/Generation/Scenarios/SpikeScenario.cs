namespace SampleLog.Generation.Scenarios;

public sealed class SpikeScenario(LogGenerator generator, string templateId, int count, int durationSeconds) : IScenario
{
    public string Name => $"Spike [{templateId}]";
    public string Description => $"{count} in {durationSeconds}s";

    public async Task RunAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromMilliseconds(durationSeconds * 1000.0 / count);
        for (int i = 0; i < count && !ct.IsCancellationRequested; i++)
        {
            generator.EmitTemplate(templateId);
            try
            {
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
