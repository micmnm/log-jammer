using LogJammer.Core.Enums;
using Pgvector;

namespace LogJammer.Core.Entities;

public class KnownError
{
    public Guid Id { get; set; }
    public required string FingerprintHash { get; set; }
    public required string RepresentativeMessage { get; set; }
    public string? RepresentativeStackTrace { get; set; }
    public Vector? EmbeddingVector { get; set; }
    public ErrorSeverity Severity { get; set; } = ErrorSeverity.Warning;
    public ErrorStatus Status { get; set; } = ErrorStatus.Active;
    public DateTime FirstSeen { get; set; }
    public DateTime LastSeen { get; set; }
    public long TotalOccurrences { get; set; }
    public string? OccurrenceWindows { get; set; } // JSON
    public Guid? DataSourceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public DataSource DataSource { get; set; } = null!;
    public ICollection<ErrorTag> ErrorTags { get; set; } = [];
    public ICollection<ErrorOccurrence> Occurrences { get; set; } = [];
    public ICollection<UserOverride> UserOverrides { get; set; } = [];
    public ICollection<Alert> Alerts { get; set; } = [];
    public ICollection<FingerprintAlias> FingerprintAliases { get; set; } = [];
}
