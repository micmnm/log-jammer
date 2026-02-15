using System.Diagnostics;

namespace SampleLog.Generation.Scenarios;

public sealed class VolumeScenario(LogGenerator generator, int targetRatePerSecond, int durationSeconds) : IScenario
{
    public string Name => $"Volume [{targetRatePerSecond}/sec]";
    public string Description => $"{targetRatePerSecond}/sec for {durationSeconds}s";
    public double ActualRate { get; private set; }

    public async Task RunAsync(CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();
        var totalMs = durationSeconds * 1000.0;
        long emitted = 0;

        while (!ct.IsCancellationRequested && stopwatch.ElapsedMilliseconds < totalMs)
        {
            // Calculate how many should have been emitted by now
            var elapsed = stopwatch.ElapsedMilliseconds;
            var expected = (long)(elapsed / 1000.0 * targetRatePerSecond);

            // Emit to catch up
            while (emitted < expected && !ct.IsCancellationRequested)
            {
                generator.EmitRandom();
                emitted++;
            }

            ActualRate = elapsed > 0 ? emitted / (elapsed / 1000.0) : 0;

            // Small yield to prevent CPU spin
            try { await Task.Delay(1, ct); }
            catch (OperationCanceledException) { break; }
        }
    }
}
