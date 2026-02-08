namespace LogJammer.Core.Models;

public record ErrorBatch(
    IReadOnlyList<RawLogEntry> Entries,
    int TotalAvailable,
    double SampleRatio);
