namespace LogJammer.Engine.Data.Entities;

public class PatternBaseline
{
    public Guid Id { get; set; }

    public Guid PatternId { get; set; }
    public LogPattern Pattern { get; set; } = null!;

    public int HourOfWeek { get; set; }

    public double AvgCount { get; set; }
    public double StdDevCount { get; set; }
}
