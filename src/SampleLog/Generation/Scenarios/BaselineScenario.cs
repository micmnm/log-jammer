namespace SampleLog.Generation.Scenarios;

public sealed class BaselineScenario(LogGenerator generator, int initialRatePerSecond) : IScenario
{
    private int _ratePerSecond = initialRatePerSecond;

    public string Name => "Baseline";
    public string Description => $"Random logs at {RatePerSecond}/sec";

    public int RatePerSecond
    {
        get => Volatile.Read(ref _ratePerSecond);
        set => Volatile.Write(ref _ratePerSecond, value);
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var interval = TimeSpan.FromMilliseconds(1000.0 / RatePerSecond);
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
