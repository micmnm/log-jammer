namespace LogJammer.Core.Entities;

public class ClassificationQueueItem
{
    public Guid Id { get; set; }
    public Guid KnownErrorId { get; set; }
    public string? SuggestedTags { get; set; } // JSON array
    public double? Confidence { get; set; }
    public bool Reviewed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public KnownError KnownError { get; set; } = null!;
}
