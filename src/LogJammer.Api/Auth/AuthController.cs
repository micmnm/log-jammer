using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LogJammer.Api.Auth;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token);

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IOptions<AuthSettings> authSettings) : ControllerBase
{
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var settings = authSettings.Value;

        if (request.Username != settings.Username || request.Password != settings.Password)
            return Unauthorized();

        return Ok(new LoginResponse(settings.ApiToken));
    }
}
