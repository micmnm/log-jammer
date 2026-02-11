namespace LogJammer.Api.Dtos;

public class TagResponse
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string TagType { get; set; }
    public string? Color { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTagRequest
{
    public required string Name { get; set; }
    public string TagType { get; set; } = "user";
    public string? Color { get; set; }
}

public class UpdateTagRequest
{
    public string? Name { get; set; }
    public string? Color { get; set; }
}
