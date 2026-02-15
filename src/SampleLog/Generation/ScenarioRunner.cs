namespace SampleLog.Generation;

public sealed class ScenarioRunner
{
    private readonly Dictionary<string, (Task Task, CancellationTokenSource Cts, Scenarios.IScenario Scenario)> _running = [];

    public IReadOnlyDictionary<string, Scenarios.IScenario> ActiveScenarios =>
        _running.ToDictionary(kv => kv.Key, kv => kv.Value.Scenario);

    public void Start(Scenarios.IScenario scenario)
    {
        if (_running.ContainsKey(scenario.Name))
            Stop(scenario.Name);

        var cts = new CancellationTokenSource();
        var task = Task.Run(() => scenario.RunAsync(cts.Token));
        _running[scenario.Name] = (task, cts, scenario);
    }

    public void Stop(string name)
    {
        if (_running.TryGetValue(name, out var entry))
        {
            entry.Cts.Cancel();
            entry.Cts.Dispose();
            _running.Remove(name);
        }
    }

    public void StopAll()
    {
        foreach (var name in _running.Keys.ToList())
            Stop(name);
    }
}
