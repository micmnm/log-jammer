using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class LogPattern
{
    public Guid Id { get; set; }

    [MaxLength(2000)]
    public required string Template { get; set; }

    public int ClusterId { get; set; }

    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }

    [MaxLength(4000)]
    public required string SampleMessage { get; set; }

    public Severity Severity { get; set; }

    public Guid DataSourceId { get; set; }
    public DataSource DataSource { get; set; } = null!;

    public bool IsNew { get; set; } = true;

    public ICollection<PatternOccurrence> Occurrences { get; set; } = [];
    public ICollection<PatternBaseline> Baselines { get; set; } = [];
}
