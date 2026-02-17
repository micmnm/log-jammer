using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using LogJammer.Core.Enums;

namespace LogJammer.Api.Dtos;

public record CreateDataSourceRequest
{
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    [Required]
    public required AdapterType AdapterType { get; init; }

    [Required]
    public required string ConnectionConfig { get; init; }

    [Range(5, 86400)]
    public int PollIntervalSeconds { get; init; } = 30;

    public string? SchemaMapping { get; init; }

    [Range(1, 10000)]
    public int SamplingBudget { get; init; } = 500;

    public bool Enabled { get; init; } = true;
}

public record UpdateDataSourceRequest
{
    [MaxLength(200)]
    public string? Name { get; init; }

    public AdapterType? AdapterType { get; init; }

    public string? ConnectionConfig { get; init; }

    [Range(5, 86400)]
    public int? PollIntervalSeconds { get; init; }

    public string? SchemaMapping { get; init; }

    [Range(1, 10000)]
    public int? SamplingBudget { get; init; }

    public bool? Enabled { get; init; }
}

public record DataSourceResponse
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public AdapterType AdapterType { get; init; }
    public required string ConnectionConfig { get; init; }
    public int PollIntervalSeconds { get; init; }
    public string? SchemaMapping { get; init; }
    public int SamplingBudget { get; init; }
    public bool Enabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public IReadOnlyList<FingerprintConfigResponse> FingerprintConfigs { get; init; } = [];
}

public record ConnectionTestResponse
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    [JsonPropertyName("latencyMs")]
    public double LatencyMs { get; init; }

    public Dictionary<string, object?>? Metadata { get; init; }
}

public record SchemaResponse
{
    public required IReadOnlyList<FieldDefinitionDto> Fields { get; init; }
}

public record FieldDefinitionDto
{
    public required string Name { get; init; }
    public required string Type { get; init; }
    public bool IsNullable { get; init; }
}

public record SampleRecordsResponse
{
    public required IReadOnlyList<RawLogEntryDto> Records { get; init; }
}

public record RawLogEntryDto
{
    public DateTime Timestamp { get; init; }
    public required Dictionary<string, object?> Fields { get; init; }
}

public record DeletionImpactResponse
{
    public int ErrorGroupCount { get; init; }
    public int OccurrenceCount { get; init; }
    public int AlertCount { get; init; }
    public int ClassificationQueueCount { get; init; }
    public int TagCount { get; init; }
    public int RuleCount { get; init; }
}

public record DiscoverIndicesRequest
{
    [Required]
    public required string ConnectionConfig { get; init; }

    public bool ShowConcreteIndices { get; init; } = false;
}

public record DiscoverSchemaRequest
{
    [Required]
    public required string ConnectionConfig { get; init; }
}

public record DiscoverIndicesResponse
{
    public required IReadOnlyList<AliasInfo> Aliases { get; init; }
    public required IReadOnlyList<DataStreamInfo> DataStreams { get; init; }
    public IReadOnlyList<string> ConcreteIndices { get; init; } = [];
}

public record AliasInfo
{
    public required string Name { get; init; }
    public required IReadOnlyList<string> Indices { get; init; }
}

public record DataStreamInfo
{
    public required string Name { get; init; }
    public int BackingIndices { get; init; }
}
