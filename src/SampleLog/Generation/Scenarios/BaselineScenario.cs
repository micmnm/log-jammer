namespace SampleLog.Generation.Scenarios;

public sealed class BaselineScenario(LogGenerator generator, int infoRate, int warnRate, int errorRate) : IScenario
{
    private int _infoRate = infoRate;
    private int _warnRate = warnRate;
    private int _errorRate = errorRate;

    public string Name => "Baseline";
    public string Description => $"INF:{InfoRate}/s WRN:{WarnRate}/s ERR:{ErrorRate}/s";

    public int InfoRate
    {
        get => Volatile.Read(ref _infoRate);
        set => Volatile.Write(ref _infoRate, value);
    }

    public int WarnRate
    {
        get => Volatile.Read(ref _warnRate);
        set => Volatile.Write(ref _warnRate, value);
    }

    public int ErrorRate
    {
        get => Volatile.Read(ref _errorRate);
        set => Volatile.Write(ref _errorRate, value);
    }

    public int TotalRate => InfoRate + WarnRate + ErrorRate;

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var total = TotalRate;
            if (total <= 0)
            {
                try { await Task.Delay(100, ct); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            var interval = TimeSpan.FromMilliseconds(1000.0 / total);

            // Weighted random pick by level
            var roll = Random.Shared.Next(total);
            var info = InfoRate;
            var warn = WarnRate;

            if (roll < info)
                generator.EmitRandomAtLevel("Information");
            else if (roll < info + warn)
                generator.EmitRandomAtLevel("Warning");
            else
                generator.EmitRandomAtLevel("Error");

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
