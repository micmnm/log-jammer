using LogJammer.Api.Auth;
using LogJammer.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(TokenService tokenService, IOptions<AuthSettings> settings) : ControllerBase
{
    private readonly AuthSettings _settings = settings.Value;

    [HttpPost("login")]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        if (request.Password != _settings.Password)
            return Unauthorized(new { message = "Invalid password" });

        var token = tokenService.CreateToken();
        return Ok(new LoginResponse(token));
    }
}
