using LogJammer.Api.Dtos;
using LogJammer.Core.Enums;
using LogJammer.Core.Interfaces;

namespace LogJammer.Api.Services;

public class AlertService(
    IAlertRepository alertRepo,
    ICorrelatedSpikeAlertRepository correlatedRepo,
    IAlertManager alertManager) : IAlertService
{
    public async Task<AlertListResponse> GetAllAsync(AlertStatus? status = null, Guid? dataSourceId = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var items = await alertRepo.GetAllAsync(status, dataSourceId, page, pageSize, cancellationToken);
        var totalCount = await alertRepo.GetCountAsync(status, dataSourceId, cancellationToken);

        return new AlertListResponse
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<AlertDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var alert = await alertRepo.GetByIdAsync(id, cancellationToken);
        return alert is null ? null : MapToDto(alert);
    }

    public async Task<AlertDto?> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await alertManager.AcknowledgeAsync(id, cancellationToken);
        var alert = await alertRepo.GetByIdAsync(id, cancellationToken);
        return alert is null ? null : MapToDto(alert);
    }

    public async Task<AlertListResponse> GetHistoryAsync(Guid? dataSourceId = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var items = await alertRepo.GetAllAsync(AlertStatus.Resolved, dataSourceId, page, pageSize, cancellationToken);
        var totalCount = await alertRepo.GetCountAsync(AlertStatus.Resolved, dataSourceId, cancellationToken);

        return new AlertListResponse
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<CorrelatedSpikeAlertDto>> GetCorrelatedAlertsAsync(AlertStatus? status = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var items = await correlatedRepo.GetAllAsync(status, page, pageSize, cancellationToken);
        return items.Select(c => new CorrelatedSpikeAlertDto
        {
            Id = c.Id,
            DataSourceId = c.DataSourceId,
            DataSourceName = c.DataSource?.Name,
            Status = c.Status,
            AlertIds = c.AlertIds,
            GroupCount = c.GroupCount,
            DetectedAt = c.DetectedAt,
            ResolvedAt = c.ResolvedAt,
            CreatedAt = c.CreatedAt
        }).ToList();
    }

    private static AlertDto MapToDto(Core.Entities.Alert a) => new()
    {
        Id = a.Id,
        KnownErrorId = a.KnownErrorId,
        KnownErrorMessage = a.KnownError?.RepresentativeMessage,
        Status = a.Status,
        ThresholdType = a.ThresholdType,
        ThresholdValue = a.ThresholdValue,
        ActualValue = a.ActualValue,
        NotificationCount = a.NotificationCount,
        LastNotifiedAt = a.LastNotifiedAt,
        AcknowledgedAt = a.AcknowledgedAt,
        ResolvedAt = a.ResolvedAt,
        CreatedAt = a.CreatedAt
    };
}
