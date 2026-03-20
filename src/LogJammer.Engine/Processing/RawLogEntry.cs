namespace LogJammer.Engine.Processing;

public class RawLogEntry
{
    public required string Message { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Level { get; set; }
    public Dictionary<string, string>? Fields { get; set; }
}
