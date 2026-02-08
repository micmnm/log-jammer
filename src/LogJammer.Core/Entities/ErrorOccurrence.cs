namespace LogJammer.Core.Entities;

public class ErrorOccurrence
{
    public Guid Id { get; set; }
    public Guid KnownErrorId { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime WindowEnd { get; set; }
    public long Count { get; set; }
    public double? SampleRatio { get; set; }
    public DateTime CreatedAt { get; set; }

    public KnownError KnownError { get; set; } = null!;
}
