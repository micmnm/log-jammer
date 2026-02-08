using LogJammer.Core.Enums;

namespace LogJammer.Core.Entities;

public class DataSource
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public AdapterType AdapterType { get; set; }
    public required string ConnectionConfig { get; set; } // JSON
    public int PollIntervalSeconds { get; set; } = 30;
    public string? SchemaMapping { get; set; } // JSON
    public int SamplingBudget { get; set; } = 500;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<FingerprintConfig> FingerprintConfigs { get; set; } = [];
    public ICollection<KnownError> KnownErrors { get; set; } = [];
}
