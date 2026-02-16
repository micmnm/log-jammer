namespace SampleLog.Models;

public sealed class OutputConfig
{
    public string Directory { get; set; } = "./logs";
    public string FilePrefix { get; set; } = "sample";
    public int RollingSizeMB { get; set; } = 10;
    public int MaxFiles { get; set; } = 5;
}

public sealed class DefaultsConfig
{
    public bool BaselineEnabled { get; set; } = true;
    public int InfoRatePerSecond { get; set; } = 2;
    public int WarnRatePerSecond { get; set; } = 0;
    public int ErrorRatePerSecond { get; set; } = 0;
    public int SpikeCount { get; set; } = 50;
    public int SpikeDurationSeconds { get; set; } = 10;
    public int DegradationDurationSeconds { get; set; } = 120;
}

public sealed class LogJammerApiConfig
{
    public string BaseUrl { get; set; } = "http://localhost:5050";
}
