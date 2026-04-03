using LogJammer.Api.Auth;
using LogJammer.Api.Dtos;
using LogJammer.Engine.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(LogJammerDbContext db, TokenService tokenService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> List()
    {
        var isAdmin = HttpContext.Items["IsAdmin"] as bool? ?? false;
        if (!isAdmin) return Forbid();

        var users = await db.Users
            .OrderBy(u => u.CreatedAt)
            .Select(u => new UserResponse(
                u.Id,
                u.Username,
                u.DisplayName,
                u.IsAdmin,
                u.CanInvite,
                u.IsDisabled,
                u.CreatedAt,
                db.Invites
                    .Where(i => i.UsedByUserId == u.Id)
                    .Select(i => i.CreatedBy.Username)
                    .FirstOrDefault()))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var isAdmin = HttpContext.Items["IsAdmin"] as bool? ?? false;
        if (!isAdmin) return Forbid();

        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        if (request.CanInvite.HasValue)
            user.CanInvite = request.CanInvite.Value;

        if (request.IsDisabled.HasValue)
        {
            user.IsDisabled = request.IsDisabled.Value;
            if (user.IsDisabled)
                tokenService.InvalidateUser(user.Id);
        }

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var isAdmin = HttpContext.Items["IsAdmin"] as bool? ?? false;
        if (!isAdmin) return Forbid();

        var currentUserId = (Guid)HttpContext.Items["UserId"]!;
        if (id == currentUserId)
            return BadRequest(new { message = "Cannot delete yourself" });

        var user = await db.Users.FindAsync(id);
        if (user is null) return NotFound();

        tokenService.InvalidateUser(user.Id);
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
