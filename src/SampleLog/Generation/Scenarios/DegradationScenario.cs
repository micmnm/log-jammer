using System.Diagnostics;

namespace SampleLog.Generation.Scenarios;

public sealed class DegradationScenario(LogGenerator generator, int startRate, int endRate, int durationSeconds) : IScenario
{
    public string Name => "Degradation";
    public string Description => $"{startRate} \u2192 {endRate}/sec over {durationSeconds}s";

    public async Task RunAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var totalMs = durationSeconds * 1000.0;

        while (!ct.IsCancellationRequested && stopwatch.ElapsedMilliseconds < totalMs)
        {
            var progress = Math.Min(stopwatch.ElapsedMilliseconds / totalMs, 1.0);
            var currentRate = startRate + (endRate - startRate) * progress;
            var interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(currentRate, 1));

            generator.EmitRandom();
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
