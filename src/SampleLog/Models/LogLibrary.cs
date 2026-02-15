using System.Text.Json.Serialization;

namespace SampleLog.Models;

public sealed class LogLibrary
{
    [JsonPropertyName("templates")]
    public required List<LogTemplate> Templates { get; init; }

    [JsonPropertyName("prebaked")]
    public required List<PrebakedEntry> Prebaked { get; init; }

    [JsonPropertyName("correlationGroups")]
    public required List<CorrelationGroup> CorrelationGroups { get; init; }
}

public sealed class CorrelationGroup
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("templateIds")]
    public required List<string> TemplateIds { get; init; }
}
