namespace LogJammer.Core.Entities;

public class Tag
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string TagType { get; set; } // "auto" or "user"
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }

    public ICollection<ErrorTag> ErrorTags { get; set; } = [];
}
