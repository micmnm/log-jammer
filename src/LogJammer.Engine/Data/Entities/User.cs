using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class User
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public required string Username { get; set; }

    [MaxLength(200)]
    public required string DisplayName { get; set; }

    public bool IsAdmin { get; set; }

    public bool CanInvite { get; set; }

    public bool IsDisabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserCredential> Credentials { get; set; } = [];
}
