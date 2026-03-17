using LogJammer.Engine.Data.Entities;

namespace LogJammer.Api.Dtos;

public record PatternListItem(
    Guid Id,
    string Template,
    Severity Severity,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    bool IsNew,
    long CurrentRate,
    double ExpectedRate,
    double StdDevsFromMean,
    string DataSourceName);

public record PatternDetailResponse(
    Guid Id,
    string Template,
    Severity Severity,
    DateTimeOffset FirstSeen,
    DateTimeOffset LastSeen,
    bool IsNew,
    long CurrentRate,
    double ExpectedRate,
    double StdDevsFromMean,
    string DataSourceName,
    string SampleMessage,
    IEnumerable<OccurrencePoint> Occurrences,
    IEnumerable<BaselineBand> BaselineBands);

public record OccurrencePoint(DateTimeOffset WindowStart, long Count);

public record BaselineBand(int HourOfWeek, double AvgCount, double StdDevCount);

public record PagedResult<T>(IEnumerable<T> Items, int TotalCount, int Page, int PageSize);
