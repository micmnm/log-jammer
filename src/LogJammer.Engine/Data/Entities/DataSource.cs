using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogJammer.Engine.Data.Entities;

public class DataSource
{
    public Guid Id { get; set; }

    [MaxLength(200)]
    public required string Name { get; set; }

    public DataSourceType Type { get; set; }

    [Column(TypeName = "jsonb")]
    public required string ConnectionConfig { get; set; }

    [MaxLength(500)]
    public string? MessageTemplate { get; set; }

    public bool Enabled { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastPolledAt { get; set; }

    [ConcurrencyCheck]
    public int Version { get; set; } = 1;

    public DrainState? DrainState { get; set; }
    public ICollection<LogPattern> Patterns { get; set; } = [];
}
