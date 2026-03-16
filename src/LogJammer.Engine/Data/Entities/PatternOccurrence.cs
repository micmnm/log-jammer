namespace LogJammer.Engine.Data.Entities;

public class PatternOccurrence
{
    public Guid Id { get; set; }

    public Guid PatternId { get; set; }
    public LogPattern Pattern { get; set; } = null!;

    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }

    public long Count { get; set; }
}
