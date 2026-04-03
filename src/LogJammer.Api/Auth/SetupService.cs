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

        // Expire any unused setup tokens from previous runs
        var now = DateTimeOffset.UtcNow;
        var unusedTokens = await db.SetupTokens
            .Where(t => t.UsedAt == null && t.ExpiresAt > now)
            .ToListAsync();

        foreach (var t in unusedTokens)
            t.ExpiresAt = now;

        if (unusedTokens.Count > 0)
            await db.SaveChangesAsync();

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
