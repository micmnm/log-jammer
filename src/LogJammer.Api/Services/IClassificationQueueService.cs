using LogJammer.Api.Dtos;

namespace LogJammer.Api.Services;

public interface IClassificationQueueService
{
    Task<ClassificationQueuePagedResponse> GetPendingAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<ClassificationQueueResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ApproveAsync(Guid id, ApproveClassificationRequest request, CancellationToken cancellationToken = default);
    Task<bool> RejectAsync(Guid id, RejectClassificationRequest request, CancellationToken cancellationToken = default);
}
