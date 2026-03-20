using LogJammer.Engine.Data.Entities;

namespace LogJammer.Engine.Processing;

public static class SeverityMapper
{
    public static Severity Map(string? level) =>
        level?.ToUpperInvariant() switch
        {
            "DEBUG"       => Severity.Info,
            "TRACE"       => Severity.Info,
            "VERBOSE"     => Severity.Info,
            "INFO"        => Severity.Info,
            "INFORMATION" => Severity.Info,
            "WARN"        => Severity.Warning,
            "WARNING"     => Severity.Warning,
            "ERROR"       => Severity.Error,
            "ERR"         => Severity.Error,
            "FATAL"       => Severity.Critical,
            "CRITICAL"    => Severity.Critical,
            _             => Severity.Info,
        };
}
