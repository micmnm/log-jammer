namespace LogJammer.Core.Models;

public record ConnectionTestResult(
    bool Success,
    string? ErrorMessage,
    TimeSpan Latency,
    Dictionary<string, object?>? Metadata = null);
