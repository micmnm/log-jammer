using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Auth;

public class SetupService(IServiceScopeFactory scopeFactory, ILogger<SetupService> logger)
{
    public async Task CheckAndBootstrapAsync(string baseUrl)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();

        if (await db.Users.AnyAsync())
            return;

        // Check for existing valid setup token
        var now = DateTimeOffset.UtcNow;
        var existingToken = await db.SetupTokens
            .AnyAsync(t => t.UsedAt == null && t.ExpiresAt > now);

        if (existingToken)
        {
            logger.LogWarning("Setup token already exists but has not been used yet");
            return;
        }

        var rawToken = TokenHasher.GenerateToken();
        var hash = TokenHasher.Hash(rawToken);

        db.SetupTokens.Add(new SetupToken
        {
            TokenHash = hash,
            ExpiresAt = now.AddHours(1)
        });

        await db.SaveChangesAsync();

        var setupUrl = $"{baseUrl.TrimEnd('/')}/setup?token={rawToken}";

        logger.LogCritical(
            "\n\n" +
            "════════════════════════════════════════════════════════════\n" +
            "  LOG JAMMER — SETUP REQUIRED\n" +
            "\n" +
            "  No admin account found. Register the first admin at:\n" +
            "\n" +
            "  {SetupUrl}\n" +
            "\n" +
            "  This link expires in 1 hour.\n" +
            "════════════════════════════════════════════════════════════\n",
            setupUrl);
    }

    public async Task CheckHttpsAsync(string[] urls)
    {
        var hasInsecure = urls.Any(u =>
        {
            var uri = new Uri(u);
            return uri.Scheme == "http" && uri.Host != "localhost" && uri.Host != "127.0.0.1";
        });

        if (hasInsecure)
        {
            logger.LogWarning(
                "\n\n" +
                "════════════════════════════════════════════════════════════\n" +
                "  WARNING: WebAuthn requires HTTPS\n" +
                "\n" +
                "  Passkey authentication will not work over plain HTTP.\n" +
                "  Configure HTTPS or use a reverse proxy with TLS termination.\n" +
                "════════════════════════════════════════════════════════════\n");
        }

        await Task.CompletedTask;
    }
}
