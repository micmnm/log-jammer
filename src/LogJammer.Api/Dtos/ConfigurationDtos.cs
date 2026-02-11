namespace LogJammer.Api.Dtos;

public class ConfigurationResponse
{
    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateConfigurationRequest
{
    public required string Key { get; set; }
    public required string Value { get; set; }
}
