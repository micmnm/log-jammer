using LogJammer.Core.Enums;

namespace LogJammer.Core.Entities;

public class SpikeDetectionRule
{
    public Guid Id { get; set; }
    public Guid? KnownErrorId { get; set; }
    public ThresholdType ThresholdType { get; set; }
    public double ThresholdValue { get; set; }
    public int WindowMinutes { get; set; } = 5;
    public int LookbackMinutes { get; set; } = 1440;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public KnownError? KnownError { get; set; }
}
