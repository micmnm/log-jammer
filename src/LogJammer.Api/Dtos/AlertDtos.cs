using LogJammer.Core.Enums;

namespace LogJammer.Api.Dtos;

public record AlertDto
{
    public Guid Id { get; init; }
    public Guid KnownErrorId { get; init; }
    public string? KnownErrorMessage { get; init; }
    public AlertStatus Status { get; init; }
    public ThresholdType ThresholdType { get; init; }
    public double ThresholdValue { get; init; }
    public double ActualValue { get; init; }
    public int NotificationCount { get; init; }
    public DateTime? LastNotifiedAt { get; init; }
    public DateTime? AcknowledgedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record AlertListResponse
{
    public required IReadOnlyList<AlertDto> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public record CorrelatedSpikeAlertDto
{
    public Guid Id { get; init; }
    public Guid DataSourceId { get; init; }
    public string? DataSourceName { get; init; }
    public AlertStatus Status { get; init; }
    public string AlertIds { get; init; } = "[]";
    public int GroupCount { get; init; }
    public DateTime DetectedAt { get; init; }
    public DateTime? ResolvedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
