namespace SampleLog.Generation.Scenarios;

public interface IScenario
{
    string Name { get; }
    string Description { get; }
    Task RunAsync(CancellationToken ct);
}
