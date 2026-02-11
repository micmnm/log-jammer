using LogJammer.Api.Dtos;
using LogJammer.Core.Enums;

namespace LogJammer.Api.Services;

public interface IAlertService
{
    Task<AlertListResponse> GetAllAsync(AlertStatus? status = null, Guid? dataSourceId = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<AlertDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AlertDto?> AcknowledgeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AlertListResponse> GetHistoryAsync(Guid? dataSourceId = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CorrelatedSpikeAlertDto>> GetCorrelatedAlertsAsync(AlertStatus? status = null, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}
