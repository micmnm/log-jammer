namespace SampleLog.Generation;

public sealed class ScenarioRunner
{
    // Thread-safety: _running is only accessed from the Terminal.Gui UI thread
    // (via event handlers, timer callbacks, and Application.Invoke). No locking needed.
    private readonly Dictionary<string, (Task Task, CancellationTokenSource Cts, Scenarios.IScenario Scenario)> _running = [];

    /// <summary>
    /// Raised on the thread pool when a scenario task faults with an unhandled exception.
    /// Subscribe to this event to display errors in the TUI log view.
    /// </summary>
    public event Action<string>? OnScenarioError;

    public IReadOnlyDictionary<string, Scenarios.IScenario> ActiveScenarios =>
        _running.ToDictionary(kv => kv.Key, kv => kv.Value.Scenario);

    public void Start(Scenarios.IScenario scenario)
    {
        ClearCompleted();

        if (_running.ContainsKey(scenario.Name))
            Stop(scenario.Name);

        var cts = new CancellationTokenSource();
        var task = Task.Run(() => scenario.RunAsync(cts.Token));

        // Observe faulted tasks so exceptions are not silently swallowed.
        task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                var message = $"Scenario '{scenario.Name}' faulted: {t.Exception?.GetBaseException().Message}";
                OnScenarioError?.Invoke(message);
            }
        }, TaskScheduler.Default);

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

    /// <summary>
    /// Removes entries whose tasks have completed (finished, faulted, or canceled)
    /// and disposes their CancellationTokenSources. Call from the UI thread.
    /// </summary>
    public void ClearCompleted()
    {
        var completed = _running
            .Where(kv => kv.Value.Task.IsCompleted)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var name in completed)
        {
            if (_running.TryGetValue(name, out var entry))
            {
                entry.Cts.Dispose();
                _running.Remove(name);
            }
        }
    }
}
