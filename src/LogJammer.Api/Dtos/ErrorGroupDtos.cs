using LogJammer.Core.Enums;

namespace LogJammer.Api.Dtos;

public class ErrorGroupResponse
{
    public Guid Id { get; set; }
    public required string FingerprintHash { get; set; }
    public required string RepresentativeMessage { get; set; }
    public ErrorSeverity Severity { get; set; }
    public ErrorStatus Status { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public long TotalOccurrences { get; set; }
    public Guid DataSourceId { get; set; }
    public string? DataSourceName { get; set; }
}

public class ErrorGroupDetailResponse : ErrorGroupResponse
{
    public string? RepresentativeStackTrace { get; set; }
}

public class ErrorGroupsPagedResponse
{
    public required IReadOnlyList<ErrorGroupResponse> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ErrorOccurrenceResponse
{
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public long Count { get; set; }
    public double? SampleRatio { get; set; }
    public double ExtrapolatedCount { get; set; }
}

public class UpdateErrorGroupStatusRequest
{
    public ErrorStatus Status { get; set; }
}

public class UpdateErrorGroupSeverityRequest
{
    public ErrorSeverity Severity { get; set; }
}
