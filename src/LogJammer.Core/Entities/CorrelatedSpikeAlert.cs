using LogJammer.Core.Enums;

namespace LogJammer.Core.Entities;

public class CorrelatedSpikeAlert
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public AlertStatus Status { get; set; } = AlertStatus.Firing;
    public string AlertIds { get; set; } = "[]";
    public int GroupCount { get; set; }
    public DateTime DetectedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DataSource DataSource { get; set; } = null!;
}
