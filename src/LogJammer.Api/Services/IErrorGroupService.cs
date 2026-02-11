using LogJammer.Api.Dtos;
using LogJammer.Core.Enums;

namespace LogJammer.Api.Services;

public interface IErrorGroupService
{
    Task<ErrorGroupsPagedResponse> GetAllAsync(
        Guid? dataSourceId = null,
        ErrorStatus? status = null,
        ErrorSeverity? severity = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<ErrorGroupDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ErrorOccurrenceResponse>> GetOccurrencesAsync(
        Guid id,
        DateTime? from = null,
        DateTime? to = null,
        CancellationToken cancellationToken = default);

    Task<ErrorGroupResponse?> UpdateStatusAsync(Guid id, ErrorStatus status, CancellationToken cancellationToken = default);
    Task<ErrorGroupResponse?> UpdateSeverityAsync(Guid id, ErrorSeverity severity, CancellationToken cancellationToken = default);
}
