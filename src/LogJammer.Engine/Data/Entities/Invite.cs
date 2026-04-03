using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class Invite
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public required string TokenHash { get; set; }

    public Guid CreatedByUserId { get; set; }

    public bool GrantCanInvite { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public Guid? UsedByUserId { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public User CreatedBy { get; set; } = null!;
    public User? UsedBy { get; set; }
}
