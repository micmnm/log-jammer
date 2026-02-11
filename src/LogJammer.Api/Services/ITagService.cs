using LogJammer.Api.Dtos;

namespace LogJammer.Api.Services;

public interface ITagService
{
    Task<IReadOnlyList<TagResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TagResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TagResponse> CreateAsync(CreateTagRequest request, CancellationToken cancellationToken = default);
    Task<TagResponse?> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
