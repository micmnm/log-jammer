using LogJammer.Core.Enums;

namespace LogJammer.Core.Models;

public record MappedLogEntry(
    string Message,
    DateTime Timestamp,
    ErrorSeverity? Severity,
    string? StackTrace,
    Dictionary<string, object?> CustomFields);
