using System.ComponentModel.DataAnnotations;

namespace LogJammer.Api.Dtos;

public record IngestRequest
{
    [Required]
    [MinLength(1)]
    public required IReadOnlyList<IngestEntry> Entries { get; init; }
}

public record IngestEntry
{
    [Required]
    public DateTime Timestamp { get; init; }

    [Required]
    public required Dictionary<string, object?> Fields { get; init; }
}

public record IngestResponse
{
    public int Accepted { get; init; }
    public int Duplicates { get; init; }
    public int Failed { get; init; }
}
