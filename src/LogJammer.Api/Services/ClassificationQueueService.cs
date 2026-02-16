using System.Text.Json;
using LogJammer.Api.Dtos;
using LogJammer.Core.Entities;
using LogJammer.Core.Interfaces;
using LogJammer.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Services;

public class ClassificationQueueService(
    IClassificationQueueRepository queueRepo,
    IUserOverrideRepository overrideRepo,
    IClassificationService classificationService,
    LogJammerDbContext context) : IClassificationQueueService
{
    public async Task<ClassificationQueuePagedResponse> GetPendingAsync(int page = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        var items = await queueRepo.GetPendingAsync(page, pageSize, cancellationToken);
        var totalCount = await queueRepo.GetPendingCountAsync(cancellationToken);

        return new ClassificationQueuePagedResponse
        {
            Items = items.Select(MapToResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ClassificationQueueResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await queueRepo.GetByIdAsync(id, cancellationToken);
        return item is null ? null : MapToResponse(item);
    }

    public async Task<bool> ApproveAsync(Guid id, ApproveClassificationRequest request, CancellationToken cancellationToken = default)
    {
        var item = await queueRepo.GetByIdAsync(id, cancellationToken);
        if (item is null) return false;

        // Create ErrorTag records for approved tags
        foreach (var tagId in request.TagIds)
        {
            var exists = await context.ErrorTags
                .AnyAsync(et => et.KnownErrorId == item.KnownErrorId && et.TagId == tagId, cancellationToken);

            if (!exists)
            {
                context.ErrorTags.Add(new ErrorTag
                {
                    KnownErrorId = item.KnownErrorId,
                    TagId = tagId,
                    IsAutoAssigned = false,
                    Confidence = 1.0
                });
            }
        }

        item.Reviewed = true;
        item.ReviewedAt = DateTime.UtcNow;
        await queueRepo.UpdateAsync(item, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Recalculate centroids for affected tags
        foreach (var tagId in request.TagIds)
        {
            await classificationService.RecalculateTagCentroidAsync(tagId, cancellationToken);
        }

        return true;
    }

    public async Task<bool> RejectAsync(Guid id, RejectClassificationRequest request, CancellationToken cancellationToken = default)
    {
        var item = await queueRepo.GetByIdAsync(id, cancellationToken);
        if (item is null) return false;

        // Remove any auto-assigned tags for this error
        var autoTags = await context.ErrorTags
            .Where(et => et.KnownErrorId == item.KnownErrorId && et.IsAutoAssigned)
            .ToListAsync(cancellationToken);
        context.ErrorTags.RemoveRange(autoTags);

        // Assign correct tags
        foreach (var tagId in request.CorrectTagIds)
        {
            var exists = await context.ErrorTags
                .AnyAsync(et => et.KnownErrorId == item.KnownErrorId && et.TagId == tagId, cancellationToken);

            if (!exists)
            {
                context.ErrorTags.Add(new ErrorTag
                {
                    KnownErrorId = item.KnownErrorId,
                    TagId = tagId,
                    IsAutoAssigned = false,
                    Confidence = 1.0
                });
            }
        }

        // Create user override to pin classification
        await overrideRepo.AddAsync(new UserOverride
        {
            KnownErrorId = item.KnownErrorId,
            OverrideType = "classification",
            OverrideData = JsonSerializer.Serialize(request.CorrectTagIds),
            Reason = request.Reason
        }, cancellationToken);

        item.Reviewed = true;
        item.ReviewedAt = DateTime.UtcNow;
        await queueRepo.UpdateAsync(item, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        // Recalculate centroids for affected tags
        var allTagIds = autoTags.Select(t => t.TagId).Union(request.CorrectTagIds).Distinct();
        foreach (var tagId in allTagIds)
        {
            await classificationService.RecalculateTagCentroidAsync(tagId, cancellationToken);
        }

        return true;
    }

    private static ClassificationQueueResponse MapToResponse(ClassificationQueueItem item)
    {
        var suggestedTags = new List<TagSuggestionResponse>();
        if (item.SuggestedTags is not null)
        {
            try
            {
                suggestedTags = JsonSerializer.Deserialize<List<TagSuggestionResponse>>(item.SuggestedTags,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
            catch
            {
                // Ignore malformed JSON
            }
        }

        return new ClassificationQueueResponse
        {
            Id = item.Id,
            KnownErrorId = item.KnownErrorId,
            Message = item.KnownError?.RepresentativeMessage ?? "",
            StackTrace = item.KnownError?.RepresentativeStackTrace,
            SuggestedTags = suggestedTags,
            Confidence = item.Confidence,
            Severity = item.KnownError?.Severity ?? Core.Enums.ErrorSeverity.Warning,
            Status = item.KnownError?.Status ?? Core.Enums.ErrorStatus.Active,
            FirstSeen = item.KnownError?.FirstSeen ?? item.CreatedAt,
            LastSeen = item.KnownError?.LastSeen ?? item.CreatedAt,
            TotalOccurrences = item.KnownError?.TotalOccurrences ?? 0,
            CreatedAt = item.CreatedAt
        };
    }
}
