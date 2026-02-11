using LogJammer.Core.Enums;

namespace LogJammer.Core.Entities;

public class Alert
{
    public Guid Id { get; set; }
    public Guid KnownErrorId { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Firing;
    public ThresholdType ThresholdType { get; set; }
    public double ThresholdValue { get; set; }
    public double ActualValue { get; set; }
    public int NotificationCount { get; set; }
    public DateTime? LastNotifiedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int ConsecutiveBelowThreshold { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public KnownError KnownError { get; set; } = null!;
}
