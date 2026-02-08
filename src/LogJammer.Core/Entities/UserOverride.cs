namespace LogJammer.Core.Entities;

public class UserOverride
{
    public Guid Id { get; set; }
    public Guid KnownErrorId { get; set; }
    public required string OverrideType { get; set; } // tag, severity, status, fingerprint, classification
    public required string OverrideData { get; set; } // JSON
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }

    public KnownError KnownError { get; set; } = null!;
}
