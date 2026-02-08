namespace LogJammer.Core.Entities;

public class ErrorTag
{
    public Guid KnownErrorId { get; set; }
    public Guid TagId { get; set; }
    public bool IsAutoAssigned { get; set; }
    public double? Confidence { get; set; }
    public DateTime CreatedAt { get; set; }

    public KnownError KnownError { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
