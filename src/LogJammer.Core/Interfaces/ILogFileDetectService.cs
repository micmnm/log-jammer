namespace LogJammer.Core.Interfaces;

public record DetectResult
{
    public required string DetectedFormat { get; init; }
    public required IReadOnlyList<DetectedField> Fields { get; init; }
    public required IReadOnlyList<Dictionary<string, object?>> SampleRecords { get; init; }
    public required DetectedConfig ProposedConfig { get; init; }
}

public record DetectedField
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? ProposedRole { get; init; } // "Timestamp", "Level", "Message", or null
}

public record DetectedConfig
{
    public required string FilePath { get; init; }
    public required string ParseMode { get; init; }
    public string? TimestampField { get; init; }
    public string? LevelField { get; init; }
    public string? MessageField { get; init; }
    public string? RegexPattern { get; init; }
}

public interface ILogFileDetectService
{
    Task<DetectResult> DetectAsync(string filePath, CancellationToken cancellationToken = default);
}
