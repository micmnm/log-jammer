using System.ComponentModel.DataAnnotations;

namespace LogJammer.Api.Dtos;

public record FingerprintConfigResponse
{
    public Guid Id { get; init; }
    public Guid DataSourceId { get; init; }
    public required string FieldName { get; init; }
    public int Order { get; init; }
    public bool NormalizeBeforeHash { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record CreateFingerprintConfigRequest
{
    [Required]
    [MaxLength(200)]
    public required string FieldName { get; init; }

    [Range(0, 100)]
    public int Order { get; init; }

    public bool NormalizeBeforeHash { get; init; } = true;
}

public record UpdateFingerprintConfigRequest
{
    [MaxLength(200)]
    public string? FieldName { get; init; }

    [Range(0, 100)]
    public int? Order { get; init; }

    public bool? NormalizeBeforeHash { get; init; }
}
