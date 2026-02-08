using System.Text.Json.Serialization;

namespace LogJammer.Infrastructure.Adapters.Elasticsearch;

public record ElasticsearchConnectionConfig
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    [JsonPropertyName("indexPattern")]
    public required string IndexPattern { get; init; }

    [JsonPropertyName("auth")]
    public ElasticsearchAuthConfig? Auth { get; init; }

    [JsonPropertyName("queryFilter")]
    public string? QueryFilter { get; init; }
}

public record ElasticsearchAuthConfig
{
    [JsonPropertyName("type")]
    public required string Type { get; init; } // "basic" or "apiKey"

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("password")]
    public string? Password { get; init; }

    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; init; }
}
