using LogJammer.Engine.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LogJammer.Api.Auth;

public class AuthMiddleware(RequestDelegate next, TokenService tokenService, IOptions<AuthSettings> settings)
{
    private static readonly string[] PublicPaths =
    [
        "/api/auth/status",
        "/api/auth/setup/options",
        "/api/auth/setup/register",
        "/api/auth/webauthn/login-options",
        "/api/auth/webauthn/login",
    ];

    private readonly AuthSettings _settings = settings.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (path.StartsWith("/scalar/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Check public WebAuthn/setup paths
        foreach (var publicPath in PublicPaths)
        {
            if (path.Equals(publicPath, StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }
        }

        // Check public path prefixes (invite registration: /api/invites/{token}/register and /api/invites/{token}/complete)
        if (path.StartsWith("/api/invites/", StringComparison.OrdinalIgnoreCase) &&
            (path.EndsWith("/register", StringComparison.OrdinalIgnoreCase) ||
             path.EndsWith("/complete", StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        // Check Authorization: Bearer {token}
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var userId = tokenService.ValidateToken(token);
            if (userId.HasValue)
            {
                var db = context.RequestServices.GetRequiredService<LogJammerDbContext>();
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
                if (user is not null && !user.IsDisabled)
                {
                    context.Items["UserId"] = userId.Value;
                    context.Items["IsAdmin"] = user.IsAdmin;
                    context.Items["CanInvite"] = user.CanInvite;
                    await next(context);
                    return;
                }
            }
        }

        // Check X-Api-Key header
        var apiKey = context.Request.Headers["X-Api-Key"].ToString();
        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(_settings.ApiKey) && apiKey == _settings.ApiKey)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized");
    }
}
