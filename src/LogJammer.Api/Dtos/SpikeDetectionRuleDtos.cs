using System.ComponentModel.DataAnnotations;
using LogJammer.Core.Enums;

namespace LogJammer.Api.Dtos;

public record SpikeDetectionRuleDto
{
    public Guid Id { get; init; }
    public Guid? KnownErrorId { get; init; }
    public string? KnownErrorMessage { get; init; }
    public ThresholdType ThresholdType { get; init; }
    public double ThresholdValue { get; init; }
    public int WindowMinutes { get; init; }
    public int LookbackMinutes { get; init; }
    public bool Enabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record CreateSpikeDetectionRuleRequest
{
    public Guid? KnownErrorId { get; init; }

    [Required]
    public required ThresholdType ThresholdType { get; init; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public required double ThresholdValue { get; init; }

    [Range(1, 1440)]
    public int WindowMinutes { get; init; } = 5;

    [Range(5, 10080)]
    public int LookbackMinutes { get; init; } = 1440;

    public bool Enabled { get; init; } = true;
}

public record UpdateSpikeDetectionRuleRequest
{
    public ThresholdType? ThresholdType { get; init; }

    [Range(0.01, double.MaxValue)]
    public double? ThresholdValue { get; init; }

    [Range(1, 1440)]
    public int? WindowMinutes { get; init; }

    [Range(5, 10080)]
    public int? LookbackMinutes { get; init; }

    public bool? Enabled { get; init; }
}
