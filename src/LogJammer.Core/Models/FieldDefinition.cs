namespace LogJammer.Core.Models;

public record FieldDefinition(
    string Name,
    string Type,
    bool IsNullable);
