namespace LogJammer.Engine.Data.Entities;

public class DrainState
{
    public Guid Id { get; set; }

    public Guid DataSourceId { get; set; }
    public DataSource DataSource { get; set; } = null!;

    public required byte[] SerializedState { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
