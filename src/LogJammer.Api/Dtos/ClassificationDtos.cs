using LogJammer.Core.Enums;

namespace LogJammer.Api.Dtos;

public class ClassificationQueueResponse
{
    public Guid Id { get; set; }
    public Guid KnownErrorId { get; set; }
    public required string Message { get; set; }
    public string? StackTrace { get; set; }
    public List<TagSuggestionResponse> SuggestedTags { get; set; } = [];
    public double? Confidence { get; set; }
    public ErrorSeverity Severity { get; set; }
    public ErrorStatus Status { get; set; }
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public long TotalOccurrences { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? DataSourceId { get; set; }
    public string? DataSourceName { get; set; }
}

public class TagSuggestionResponse
{
    public Guid TagId { get; set; }
    public required string TagName { get; set; }
    public double Confidence { get; set; }
}

public class ClassificationQueuePagedResponse
{
    public required IReadOnlyList<ClassificationQueueResponse> Items { get; set; }
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public class ApproveClassificationRequest
{
    public required List<Guid> TagIds { get; set; }
}

public class RejectClassificationRequest
{
    public required List<Guid> CorrectTagIds { get; set; }
    public string? Reason { get; set; }
}
