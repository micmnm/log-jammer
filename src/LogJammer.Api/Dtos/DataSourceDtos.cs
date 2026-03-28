using LogJammer.Engine.Data.Entities;

namespace LogJammer.Api.Dtos;

public record CreateDataSourceRequest(
    string Name,
    DataSourceType Type,
    string ConnectionConfig,
    string? MessageTemplate);

public record UpdateDataSourceRequest(
    string? Name,
    string? ConnectionConfig,
    string? MessageTemplate,
    bool? Enabled,
    int Version);

public record DataSourceResponse(
    Guid Id,
    string Name,
    DataSourceType Type,
    string ConnectionConfig,
    string? MessageTemplate,
    bool Enabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastPolledAt,
    int Version);

public record FieldInfo(string Name, string? SampleValue);
