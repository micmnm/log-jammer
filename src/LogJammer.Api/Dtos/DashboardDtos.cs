using LogJammer.Engine.Data.Entities;

namespace LogJammer.Api.Dtos;

public record DashboardResponse(
    int TotalPatterns,
    int NewPatternCount,
    long IngestionRatePerHour,
    IEnumerable<AnomalyItem> TopAnomalies,
    IEnumerable<NewPatternItem> NewPatterns);

public record AnomalyItem(
    Guid PatternId,
    string Template,
    Severity Severity,
    long CurrentRate,
    double ExpectedRate,
    double StdDevsFromMean,
    string DataSourceName);

public record NewPatternItem(
    Guid PatternId,
    string Template,
    Severity Severity,
    DateTimeOffset FirstSeen,
    string DataSourceName);
