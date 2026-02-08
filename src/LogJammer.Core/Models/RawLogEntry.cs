namespace LogJammer.Core.Models;

public record RawLogEntry(
    DateTime Timestamp,
    Dictionary<string, object?> Fields);
