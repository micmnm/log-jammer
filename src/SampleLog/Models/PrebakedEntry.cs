using System.Text.Json.Serialization;

namespace SampleLog.Models;

public sealed class PrebakedEntry
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("level")]
    public required string Level { get; init; }

    [JsonPropertyName("raw")]
    public required string Raw { get; init; }
}
