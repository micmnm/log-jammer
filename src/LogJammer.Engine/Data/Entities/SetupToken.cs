using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class SetupToken
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
