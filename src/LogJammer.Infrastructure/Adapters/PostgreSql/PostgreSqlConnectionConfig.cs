using System.Text.Json.Serialization;

namespace LogJammer.Infrastructure.Adapters.PostgreSql;

public record PostgreSqlConnectionConfig
{
    [JsonPropertyName("connectionString")]
    public required string ConnectionString { get; init; }

    [JsonPropertyName("tableName")]
    public required string TableName { get; init; }

    [JsonPropertyName("timestampColumn")]
    public string TimestampColumn { get; init; } = "timestamp";

    [JsonPropertyName("queryFilter")]
    public string? QueryFilter { get; init; }
}
