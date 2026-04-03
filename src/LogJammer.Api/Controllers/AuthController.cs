using Fido2NetLib;
using LogJammer.Api.Auth;
using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    LogJammerDbContext db,
    TokenService tokenService,
    WebAuthnService webAuthnService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<AuthStatusResponse>> GetStatus()
    {
        var initialized = await db.Users.AnyAsync();
        return Ok(new AuthStatusResponse(initialized));
    }

    [HttpPost("setup/options")]
    public async Task<IActionResult> SetupOptions([FromBody] SetupOptionsRequest request)
    {
        if (await db.Users.AnyAsync())
            return BadRequest(new { message = "Already initialized" });

        var now = DateTimeOffset.UtcNow;
        var hash = TokenHasher.Hash(request.Token);
        var setupToken = await db.SetupTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now);

        if (setupToken is null)
            return BadRequest(new { message = "Invalid or expired setup token" });

        var options = await webAuthnService.CreateRegistrationOptionsAsync(
            db, request.Username, request.DisplayName);

        HttpContext.Session.SetString("fido2.setup.options", options.ToJson());
        HttpContext.Session.SetString("fido2.setup.token", request.Token);
        HttpContext.Session.SetString("fido2.setup.username", request.Username);
        HttpContext.Session.SetString("fido2.setup.displayName", request.DisplayName);

        return Ok(options);
    }

    [HttpPost("setup/register")]
    public async Task<ActionResult<LoginResponse>> SetupRegister(
        [FromBody] AuthenticatorAttestationRawResponse attestationResponse)
    {
        if (await db.Users.AnyAsync())
            return BadRequest(new { message = "Already initialized" });

        var optionsJson = HttpContext.Session.GetString("fido2.setup.options");
        var token = HttpContext.Session.GetString("fido2.setup.token");
        var username = HttpContext.Session.GetString("fido2.setup.username");
        var displayName = HttpContext.Session.GetString("fido2.setup.displayName");

        if (optionsJson is null || token is null || username is null || displayName is null)
            return BadRequest(new { message = "No pending setup registration" });

        var options = CredentialCreateOptions.FromJson(optionsJson);

        // Validate setup token again
        var now = DateTimeOffset.UtcNow;
        var hash = TokenHasher.Hash(token);
        var setupToken = await db.SetupTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.UsedAt == null && t.ExpiresAt > now);

        if (setupToken is null)
            return BadRequest(new { message = "Setup token expired" });

        var credential = await webAuthnService.CompleteRegistrationAsync(db, attestationResponse, options);

        var user = new User
        {
            Username = username,
            DisplayName = displayName,
            IsAdmin = true,
            CanInvite = true
        };

        credential.UserId = user.Id;
        credential.DeviceInfo = Request.Headers.UserAgent.ToString();

        db.Users.Add(user);
        db.UserCredentials.Add(credential);
        setupToken.UsedAt = now;
        await db.SaveChangesAsync();

        HttpContext.Session.Clear();

        var bearerToken = tokenService.CreateToken(user.Id);
        var userInfo = new UserInfo(user.Id, user.Username, user.DisplayName, user.IsAdmin, user.CanInvite);
        return Ok(new LoginResponse(bearerToken, userInfo));
    }

    [HttpPost("webauthn/login-options")]
    public async Task<IActionResult> LoginOptions()
    {
        var options = await webAuthnService.CreateLoginOptionsAsync(db);
        HttpContext.Session.SetString("fido2.login.options", options.ToJson());
        return Ok(options);
    }

    [HttpPost("webauthn/login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] AuthenticatorAssertionRawResponse assertionResponse)
    {
        var optionsJson = HttpContext.Session.GetString("fido2.login.options");
        if (optionsJson is null)
            return BadRequest(new { message = "No pending login challenge" });

        var options = AssertionOptions.FromJson(optionsJson);

        var (credential, newSignCount) = await webAuthnService.CompleteLoginAsync(
            db, assertionResponse, options);

        credential.SignCount = newSignCount;
        await db.SaveChangesAsync();

        HttpContext.Session.Remove("fido2.login.options");

        var user = credential.User;
        var bearerToken = tokenService.CreateToken(user.Id);
        var userInfo = new UserInfo(user.Id, user.Username, user.DisplayName, user.IsAdmin, user.CanInvite);
        return Ok(new LoginResponse(bearerToken, userInfo));
    }

    [HttpPost("webauthn/register-options")]
    public async Task<IActionResult> RegisterOptions()
    {
        var userId = (Guid)HttpContext.Items["UserId"]!;
        var user = await db.Users.FindAsync(userId);
        if (user is null) return NotFound();

        var options = await webAuthnService.CreateRegistrationOptionsAsync(
            db, user.Username, user.DisplayName, user.Id);

        HttpContext.Session.SetString("fido2.register.options", options.ToJson());
        return Ok(options);
    }

    [HttpPost("webauthn/register")]
    public async Task<ActionResult<CredentialResponse>> Register(
        [FromBody] AuthenticatorAttestationRawResponse attestationResponse)
    {
        var userId = (Guid)HttpContext.Items["UserId"]!;

        var optionsJson = HttpContext.Session.GetString("fido2.register.options");
        if (optionsJson is null)
            return BadRequest(new { message = "No pending registration" });

        var options = CredentialCreateOptions.FromJson(optionsJson);
        var credential = await webAuthnService.CompleteRegistrationAsync(db, attestationResponse, options);

        credential.UserId = userId;
        credential.DeviceInfo = Request.Headers.UserAgent.ToString();
        db.UserCredentials.Add(credential);
        await db.SaveChangesAsync();

        HttpContext.Session.Remove("fido2.register.options");

        return Ok(new CredentialResponse(credential.Id, credential.DeviceInfo, credential.CreatedAt));
    }

    [HttpGet("~/api/users/me/credentials")]
    public async Task<ActionResult<List<CredentialResponse>>> GetMyCredentials()
    {
        var userId = (Guid)HttpContext.Items["UserId"]!;
        var credentials = await db.UserCredentials
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CredentialResponse(c.Id, c.DeviceInfo, c.CreatedAt))
            .ToListAsync();

        return Ok(credentials);
    }

    [HttpDelete("~/api/users/me/credentials/{id:guid}")]
    public async Task<IActionResult> DeleteMyCredential(Guid id)
    {
        var userId = (Guid)HttpContext.Items["UserId"]!;
        var credential = await db.UserCredentials
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (credential is null) return NotFound();

        var count = await db.UserCredentials.CountAsync(c => c.UserId == userId);
        if (count <= 1)
            return BadRequest(new { message = "Cannot remove last passkey" });

        db.UserCredentials.Remove(credential);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("~/api/settings/api-key")]
    public IActionResult GetApiKey([FromServices] Microsoft.Extensions.Options.IOptions<AuthSettings> settings)
    {
        return Ok(new { apiKey = settings.Value.ApiKey ?? "" });
    }
}
