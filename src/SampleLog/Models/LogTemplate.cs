using System.Text.Json.Serialization;

namespace SampleLog.Models;

public sealed class LogTemplate
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("level")]
    public required string Level { get; init; }

    [JsonPropertyName("messageTemplate")]
    public required string MessageTemplate { get; init; }

    [JsonPropertyName("sourceContext")]
    public string? SourceContext { get; init; }

    [JsonPropertyName("properties")]
    public Dictionary<string, List<object>>? Properties { get; init; }

    [JsonPropertyName("exception")]
    public string? Exception { get; init; }

    [JsonPropertyName("tags")]
    public List<string>? Tags { get; init; }
}
