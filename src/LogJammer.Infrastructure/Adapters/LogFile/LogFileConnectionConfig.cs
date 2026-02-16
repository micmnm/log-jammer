using System.Text.Json.Serialization;

namespace LogJammer.Infrastructure.Adapters.LogFile;

public record LogFileConnectionConfig
{
    [JsonPropertyName("filePath")]
    public required string FilePath { get; init; }

    [JsonPropertyName("parseMode")]
    public string ParseMode { get; init; } = "jsonlines"; // "jsonlines" or "regex"

    [JsonPropertyName("regexPattern")]
    public string? RegexPattern { get; init; }

    [JsonPropertyName("timestampField")]
    public string? TimestampField { get; init; }

    [JsonPropertyName("timestampFormat")]
    public string? TimestampFormat { get; init; }

    [JsonPropertyName("levelField")]
    public string? LevelField { get; init; }

    [JsonPropertyName("messageField")]
    public string? MessageField { get; init; }
}
