using Fido2NetLib;
using LogJammer.Api.Auth;
using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/invites")]
public class InvitesController(
    LogJammerDbContext db,
    TokenService tokenService,
    WebAuthnService webAuthnService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<InviteResponse>> Create([FromBody] CreateInviteRequest request)
    {
        var canInvite = HttpContext.Items["CanInvite"] as bool? ?? false;
        if (!canInvite)
            return Forbid();

        var userId = (Guid)HttpContext.Items["UserId"]!;
        var rawToken = TokenHasher.GenerateToken();
        var hash = TokenHasher.Hash(rawToken);

        var invite = new Invite
        {
            TokenHash = hash,
            CreatedByUserId = userId,
            GrantCanInvite = request.GrantCanInvite,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(24)
        };

        db.Invites.Add(invite);
        await db.SaveChangesAsync();

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var inviteUrl = $"{baseUrl}/register?invite={rawToken}";

        return Ok(new InviteResponse(
            invite.Id,
            invite.GrantCanInvite,
            invite.ExpiresAt,
            null,
            null,
            DateTimeOffset.UtcNow,
            inviteUrl));
    }

    [HttpGet]
    public async Task<ActionResult<List<InviteResponse>>> List()
    {
        var userId = (Guid)HttpContext.Items["UserId"]!;
        var isAdmin = HttpContext.Items["IsAdmin"] as bool? ?? false;

        var query = db.Invites
            .Include(i => i.UsedBy)
            .AsQueryable();

        if (!isAdmin)
            query = query.Where(i => i.CreatedByUserId == userId);

        var invites = await query
            .OrderByDescending(i => i.ExpiresAt)
            .Select(i => new InviteResponse(
                i.Id,
                i.GrantCanInvite,
                i.ExpiresAt,
                i.UsedBy != null ? i.UsedBy.Username : null,
                i.UsedAt,
                i.ExpiresAt.AddHours(-24),
                null))
            .ToListAsync();

        return Ok(invites);
    }

    [HttpPost("{token}/register")]
    public async Task<IActionResult> RegisterOptions(
        string token,
        [FromBody] InviteRegisterOptionsRequest request)
    {
        var now = DateTimeOffset.UtcNow;
        var hash = TokenHasher.Hash(token);
        var invite = await db.Invites
            .FirstOrDefaultAsync(i => i.TokenHash == hash && i.UsedAt == null && i.ExpiresAt > now);

        if (invite is null)
            return BadRequest(new { message = "Invalid or expired invite" });

        var options = await webAuthnService.CreateRegistrationOptionsAsync(
            db, request.Username, request.DisplayName);

        var optionsJson = options.ToJson();
        HttpContext.Session.SetString("fido2.invite.options", optionsJson);
        HttpContext.Session.SetString("fido2.invite.token", token);
        HttpContext.Session.SetString("fido2.invite.username", request.Username);
        HttpContext.Session.SetString("fido2.invite.displayName", request.DisplayName);

        return Content(optionsJson, "application/json");
    }

    [HttpPost("{token}/complete")]
    public async Task<ActionResult<LoginResponse>> CompleteRegistration(
        string token,
        [FromBody] AuthenticatorAttestationRawResponse attestationResponse)
    {
        var optionsJson = HttpContext.Session.GetString("fido2.invite.options");
        var savedToken = HttpContext.Session.GetString("fido2.invite.token");
        var username = HttpContext.Session.GetString("fido2.invite.username");
        var displayName = HttpContext.Session.GetString("fido2.invite.displayName");

        if (optionsJson is null || savedToken is null || savedToken != token ||
            username is null || displayName is null)
            return BadRequest(new { message = "No pending invite registration" });

        var options = CredentialCreateOptions.FromJson(optionsJson);

        var now = DateTimeOffset.UtcNow;
        var hash = TokenHasher.Hash(token);
        var invite = await db.Invites
            .FirstOrDefaultAsync(i => i.TokenHash == hash && i.UsedAt == null && i.ExpiresAt > now);

        if (invite is null)
            return BadRequest(new { message = "Invite expired" });

        try
        {
            // NOTE: CompleteRegistrationAsync takes db as first param (updated in Task 4)
            var credential = await webAuthnService.CompleteRegistrationAsync(db, attestationResponse, options);

            var user = new User
            {
                Username = username,
                DisplayName = displayName,
                CanInvite = invite.GrantCanInvite
            };

            credential.UserId = user.Id;
            credential.DeviceInfo = Request.Headers.UserAgent.ToString();

            db.Users.Add(user);
            db.UserCredentials.Add(credential);
            invite.UsedByUserId = user.Id;
            invite.UsedAt = now;
            await db.SaveChangesAsync();

            HttpContext.Session.Clear();

            var bearerToken = tokenService.CreateToken(user.Id);
            var userInfo = new UserInfo(user.Id, user.Username, user.DisplayName, user.IsAdmin, user.CanInvite);
            return Ok(new LoginResponse(bearerToken, userInfo));
        }
        catch (Fido2VerificationException)
        {
            return BadRequest(new { message = "Passkey verification failed" });
        }
    }
}
