namespace LogJammer.Api.Auth;

public class AuthSettings
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "changeme";
    public string ApiToken { get; set; } = Guid.NewGuid().ToString();
}
