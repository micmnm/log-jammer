namespace LogJammer.Core.Entities;

public class FingerprintConfig
{
    public Guid Id { get; set; }
    public Guid DataSourceId { get; set; }
    public required string FieldName { get; set; }
    public int Order { get; set; }
    public bool NormalizeBeforeHash { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public DataSource DataSource { get; set; } = null!;
}
