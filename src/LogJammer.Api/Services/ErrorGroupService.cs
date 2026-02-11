using LogJammer.Api.Dtos;
using LogJammer.Core.Entities;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;

namespace LogJammer.Api.Services;

public class ErrorGroupService(
    IKnownErrorRepository knownErrorRepo,
    IErrorOccurrenceRepository occurrenceRepo) : IErrorGroupService
{
    public async Task<ErrorGroupsPagedResponse> GetAllAsync(
        Guid? dataSourceId = null,
        ErrorStatus? status = null,
        ErrorSeverity? severity = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var items = await knownErrorRepo.GetAllAsync(dataSourceId, status, severity, page, pageSize, cancellationToken);
        var totalCount = await knownErrorRepo.GetCountAsync(dataSourceId, status, severity, cancellationToken);

        return new ErrorGroupsPagedResponse
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ErrorGroupDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var knownError = await knownErrorRepo.GetByIdAsync(id, cancellationToken);
        if (knownError is null) return null;

        return new ErrorGroupDetailResponse
        {
            Id = knownError.Id,
            FingerprintHash = knownError.FingerprintHash,
            RepresentativeMessage = knownError.RepresentativeMessage,
            RepresentativeStackTrace = knownError.RepresentativeStackTrace,
            Severity = knownError.Severity,
            Status = knownError.Status,
            FirstSeen = knownError.FirstSeen,
            LastSeen = knownError.LastSeen,
            TotalOccurrences = knownError.TotalOccurrences,
            DataSourceId = knownError.DataSourceId,
            DataSourceName = knownError.DataSource?.Name
        };
    }

    public async Task<IReadOnlyList<ErrorOccurrenceResponse>> GetOccurrencesAsync(
        Guid id,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var knownError = await knownErrorRepo.GetByIdAsync(id, cancellationToken);
        if (knownError is null) return [];

        var occurrences = await occurrenceRepo.GetByKnownErrorAsync(id, from, to, cancellationToken);

        return occurrences.Select(o => new ErrorOccurrenceResponse
        {
            WindowStart = o.WindowStart,
            WindowEnd = o.WindowEnd,
            Count = o.Count,
            SampleRatio = o.SampleRatio,
            ExtrapolatedCount = o.SampleRatio is > 0 and < 1
                ? o.Count / o.SampleRatio.Value
                : o.Count
        }).ToList();
    }

    public async Task<ErrorGroupResponse?> UpdateStatusAsync(Guid id, ErrorStatus status, CancellationToken cancellationToken = default)
    {
        var knownError = await knownErrorRepo.GetByIdAsync(id, cancellationToken);
        if (knownError is null) return null;

        knownError.Status = status;
        await knownErrorRepo.UpdateAsync(knownError, cancellationToken);
        return MapToResponse(knownError);
    }

    public async Task<ErrorGroupResponse?> UpdateSeverityAsync(Guid id, ErrorSeverity severity, CancellationToken cancellationToken = default)
    {
        var knownError = await knownErrorRepo.GetByIdAsync(id, cancellationToken);
        if (knownError is null) return null;

        knownError.Severity = severity;
        await knownErrorRepo.UpdateAsync(knownError, cancellationToken);
        return MapToResponse(knownError);
    }

    private static ErrorGroupResponse MapToResponse(KnownError e) => new()
    {
        Id = e.Id,
        FingerprintHash = e.FingerprintHash,
        RepresentativeMessage = e.RepresentativeMessage,
        Severity = e.Severity,
        Status = e.Status,
        FirstSeen = e.FirstSeen,
        LastSeen = e.LastSeen,
        TotalOccurrences = e.TotalOccurrences,
        DataSourceId = e.DataSourceId,
        DataSourceName = e.DataSource?.Name
    };
}
