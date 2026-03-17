using Microsoft.Extensions.Options;

namespace LogJammer.Api.Auth;

public class AuthMiddleware(RequestDelegate next, TokenService tokenService, IOptions<AuthSettings> settings)
{
    private readonly AuthSettings _settings = settings.Value;

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip auth for non-API paths (static files, frontend)
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Skip auth for specific paths
        if (path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/healthz", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/scalar/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/openapi/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Check Authorization: Bearer {token}
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            if (tokenService.ValidateToken(token))
            {
                await next(context);
                return;
            }
        }

        // Check X-Api-Key header
        var apiKey = context.Request.Headers["X-Api-Key"].ToString();
        if (!string.IsNullOrEmpty(apiKey) && apiKey == _settings.ApiKey)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized");
    }
}
