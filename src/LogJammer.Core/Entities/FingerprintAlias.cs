namespace LogJammer.Core.Entities;

public class FingerprintAlias
{
    public Guid Id { get; set; }
    public required string FingerprintHash { get; set; }
    public Guid KnownErrorId { get; set; }
    public DateTime CreatedAt { get; set; }

    public KnownError KnownError { get; set; } = null!;
}
