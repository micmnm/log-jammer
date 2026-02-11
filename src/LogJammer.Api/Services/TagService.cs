using LogJammer.Api.Dtos;
using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;

namespace LogJammer.Api.Services;

public class TagService(ITagRepository tagRepo, IClassificationService classificationService) : ITagService
{
    public async Task<IReadOnlyList<TagResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var tags = await tagRepo.GetAllAsync(cancellationToken);
        return tags.Select(MapToResponse).ToList();
    }

    public async Task<TagResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tag = await tagRepo.GetByIdAsync(id, cancellationToken);
        return tag is null ? null : MapToResponse(tag);
    }

    public async Task<TagResponse> CreateAsync(CreateTagRequest request, CancellationToken cancellationToken = default)
    {
        var tag = new Tag
        {
            Name = request.Name,
            TagType = request.TagType,
            Color = request.Color
        };

        tag = await tagRepo.AddAsync(tag, cancellationToken);
        return MapToResponse(tag);
    }

    public async Task<TagResponse?> UpdateAsync(Guid id, UpdateTagRequest request, CancellationToken cancellationToken = default)
    {
        var tag = await tagRepo.GetByIdAsync(id, cancellationToken);
        if (tag is null) return null;

        if (request.Name is not null) tag.Name = request.Name;
        if (request.Color is not null) tag.Color = request.Color;

        await tagRepo.UpdateAsync(tag, cancellationToken);
        await classificationService.RecalculateTagCentroidAsync(id, cancellationToken);
        return MapToResponse(tag);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tag = await tagRepo.GetByIdAsync(id, cancellationToken);
        if (tag is null) return false;

        await tagRepo.DeleteAsync(tag, cancellationToken);
        return true;
    }

    private static TagResponse MapToResponse(Tag tag) => new()
    {
        Id = tag.Id,
        Name = tag.Name,
        TagType = tag.TagType,
        Color = tag.Color,
        CreatedAt = tag.CreatedAt
    };
}
