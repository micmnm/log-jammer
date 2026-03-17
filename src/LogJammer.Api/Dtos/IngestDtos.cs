using System.ComponentModel.DataAnnotations;

namespace LogJammer.Api.Dtos;

public record IngestRequest(
    [MaxLength(10000)] IngestEntry[] Entries);

public record IngestEntry(
    string Message,
    DateTimeOffset Timestamp,
    string? Level);

public record IngestResponse(int Accepted);
