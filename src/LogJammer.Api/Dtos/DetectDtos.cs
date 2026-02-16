using System.ComponentModel.DataAnnotations;

namespace LogJammer.Api.Dtos;

public record DetectRequest
{
    [Required]
    public required string FilePath { get; init; }
}

public record DetectResponse
{
    public required string DetectedFormat { get; init; }
    public required IReadOnlyList<DetectedFieldDto> Fields { get; init; }
    public required IReadOnlyList<Dictionary<string, object?>> SampleRecords { get; init; }
    public required DetectedConfigDto ProposedConfig { get; init; }
}

public record DetectedFieldDto
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string? ProposedRole { get; init; }
}

public record DetectedConfigDto
{
    public required string FilePath { get; init; }
    public required string ParseMode { get; init; }
    public string? TimestampField { get; init; }
    public string? LevelField { get; init; }
    public string? MessageField { get; init; }
    public string? RegexPattern { get; init; }
}
