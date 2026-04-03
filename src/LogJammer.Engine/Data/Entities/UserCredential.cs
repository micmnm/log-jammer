using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class UserCredential
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required byte[] CredentialId { get; set; }

    public required byte[] PublicKey { get; set; }

    public uint SignCount { get; set; }

    [MaxLength(500)]
    public string? DeviceInfo { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
