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
    public int BaselineRatePerSecond { get; set; } = 2;
    public int SpikeCount { get; set; } = 50;
    public int SpikeDurationSeconds { get; set; } = 10;
    public int DegradationDurationSeconds { get; set; } = 120;
}
