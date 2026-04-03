# Passkey Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace password-only auth with WebAuthn passkeys, invite-only registration, and admin bootstrap on first startup.

**Architecture:** New EF entities (User, Credential, Invite, SetupToken) in LogJammer.Engine. Fido2NetLib handles WebAuthn ceremonies. AuthController rewritten for WebAuthn flows. Frontend gets setup page, passkey login, invite registration, user management (admin), and settings with passkey management + API key display.

**Tech Stack:** .NET 10, Fido2NetLib, EF Core 10 + PostgreSQL, React 19, @simplewebauthn/browser, MUI 7, TanStack Query 5

**Spec:** `docs/superpowers/specs/2026-04-03-passkey-auth-design.md`

---

## File Structure

### Backend — New Files
- `src/LogJammer.Engine/Data/Entities/User.cs` — User entity
- `src/LogJammer.Engine/Data/Entities/UserCredential.cs` — WebAuthn credential entity
- `src/LogJammer.Engine/Data/Entities/Invite.cs` — Invite entity
- `src/LogJammer.Engine/Data/Entities/SetupToken.cs` — Bootstrap setup token entity
- `src/LogJammer.Api/Auth/SetupService.cs` — Bootstrap logic (check if initialized, generate setup token, log it)
- `src/LogJammer.Api/Auth/WebAuthnService.cs` — Wraps Fido2NetLib for attestation/assertion ceremonies
- `src/LogJammer.Api/Auth/TokenHasher.cs` — SHA-256 hashing + secure random token generation
- `src/LogJammer.Api/Controllers/InvitesController.cs` — Invite CRUD + registration
- `src/LogJammer.Api/Controllers/UsersController.cs` — Admin user management
- `src/LogJammer.Api/Dtos/WebAuthnDtos.cs` — DTOs for all WebAuthn and auth endpoints
- `src/LogJammer.Api/Dtos/InviteDtos.cs` — DTOs for invite endpoints
- `src/LogJammer.Api/Dtos/UserDtos.cs` — DTOs for user management endpoints

### Backend — Modified Files
- `src/LogJammer.Api/LogJammer.Api.csproj` — Add Fido2NetLib packages
- `src/LogJammer.Engine/Data/LogJammerDbContext.cs` — Add DbSets + entity config for User, UserCredential, Invite, SetupToken
- `src/LogJammer.Api/Auth/TokenService.cs` — Track UserId per token, add InvalidateUser method
- `src/LogJammer.Api/Auth/AuthMiddleware.cs` — Update public path whitelist, set UserId on HttpContext
- `src/LogJammer.Api/Auth/AuthSettings.cs` — Remove required Password, keep ApiKey
- `src/LogJammer.Api/Controllers/AuthController.cs` — Rewrite: remove password login, add WebAuthn endpoints + status + setup
- `src/LogJammer.Api/Program.cs` — Register Fido2, WebAuthnService, SetupService; run bootstrap after migration
- `src/LogJammer.Api/appsettings.json` — Remove Password, add Fido2 config
- `src/LogJammer.Api/Dtos/AuthDtos.cs` — Remove LoginRequest/LoginResponse

### Frontend — New Files
- `src/frontend/src/pages/Setup.tsx` — First-time setup page
- `src/frontend/src/pages/Register.tsx` — Invite registration page
- `src/frontend/src/pages/Users.tsx` — Admin user management page
- `src/frontend/src/api/hooks/useUsers.ts` — Admin user management hooks
- `src/frontend/src/api/hooks/useInvites.ts` — Invite hooks
- `src/frontend/src/api/hooks/useCredentials.ts` — Passkey management hooks
- `src/frontend/src/api/hooks/useSetup.ts` — Setup status + registration hooks

### Frontend — Modified Files
- `src/frontend/package.json` — Add @simplewebauthn/browser
- `src/frontend/src/App.tsx` — Add routes for /setup, /register, /users; check initialized state
- `src/frontend/src/api/hooks/useAuth.ts` — Replace useLogin with usePasskeyLogin, add user context (isAdmin, canInvite)
- `src/frontend/src/api/types.ts` — Add auth/user/invite types
- `src/frontend/src/pages/Login.tsx` — Replace password form with passkey button
- `src/frontend/src/pages/Settings.tsx` — Add API key display (from endpoint) + passkey management section
- `src/frontend/src/components/Sidebar.tsx` — Add "Users" nav item (admin only)

---

## Task 1: Add NuGet Packages and EF Entities

**Files:**
- Modify: `src/LogJammer.Api/LogJammer.Api.csproj`
- Create: `src/LogJammer.Engine/Data/Entities/User.cs`
- Create: `src/LogJammer.Engine/Data/Entities/UserCredential.cs`
- Create: `src/LogJammer.Engine/Data/Entities/Invite.cs`
- Create: `src/LogJammer.Engine/Data/Entities/SetupToken.cs`
- Modify: `src/LogJammer.Engine/Data/LogJammerDbContext.cs`

- [ ] **Step 1: Add Fido2NetLib NuGet packages**

Add to `src/LogJammer.Api/LogJammer.Api.csproj`:

```xml
<PackageReference Include="Fido2" Version="4.0.0" />
<PackageReference Include="Fido2.AspNet" Version="4.0.0" />
```

Run: `cd src/LogJammer.Api && dotnet restore`

- [ ] **Step 2: Create User entity**

Create `src/LogJammer.Engine/Data/Entities/User.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class User
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public required string Username { get; set; }

    [MaxLength(200)]
    public required string DisplayName { get; set; }

    public bool IsAdmin { get; set; }

    public bool CanInvite { get; set; }

    public bool IsDisabled { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserCredential> Credentials { get; set; } = [];
}
```

- [ ] **Step 3: Create UserCredential entity**

Create `src/LogJammer.Engine/Data/Entities/UserCredential.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class UserCredential
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required byte[] CredentialId { get; set; }

    public required byte[] PublicKey { get; set; }

    public uint SignCount { get; set; }

    [MaxLength(500)]
    public string? DeviceInfo { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = null!;
}
```

- [ ] **Step 4: Create Invite entity**

Create `src/LogJammer.Engine/Data/Entities/Invite.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class Invite
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public required string TokenHash { get; set; }

    public Guid CreatedByUserId { get; set; }

    public bool GrantCanInvite { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public Guid? UsedByUserId { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public User CreatedBy { get; set; } = null!;
    public User? UsedBy { get; set; }
}
```

- [ ] **Step 5: Create SetupToken entity**

Create `src/LogJammer.Engine/Data/Entities/SetupToken.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace LogJammer.Engine.Data.Entities;

public class SetupToken
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }
}
```

- [ ] **Step 6: Register entities in DbContext**

Add DbSets and entity configuration to `src/LogJammer.Engine/Data/LogJammerDbContext.cs`.

Add these DbSet properties after the existing ones:

```csharp
public DbSet<User> Users => Set<User>();
public DbSet<UserCredential> UserCredentials => Set<UserCredential>();
public DbSet<Invite> Invites => Set<Invite>();
public DbSet<SetupToken> SetupTokens => Set<SetupToken>();
```

Add these entity configurations inside `OnModelCreating`, after the existing ones:

```csharp
modelBuilder.Entity<User>(e =>
{
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.Username).IsUnique();
});

modelBuilder.Entity<UserCredential>(e =>
{
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.CredentialId).IsUnique();
    e.HasOne(x => x.User)
        .WithMany(x => x.Credentials)
        .HasForeignKey(x => x.UserId)
        .OnDelete(DeleteBehavior.Cascade);
});

modelBuilder.Entity<Invite>(e =>
{
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.TokenHash).IsUnique();
    e.HasOne(x => x.CreatedBy)
        .WithMany()
        .HasForeignKey(x => x.CreatedByUserId)
        .OnDelete(DeleteBehavior.Cascade);
    e.HasOne(x => x.UsedBy)
        .WithMany()
        .HasForeignKey(x => x.UsedByUserId)
        .OnDelete(DeleteBehavior.SetNull);
});

modelBuilder.Entity<SetupToken>(e =>
{
    e.HasKey(x => x.Id);
    e.HasIndex(x => x.TokenHash).IsUnique();
});
```

- [ ] **Step 7: Create EF migration**

Run:
```bash
cd src/LogJammer.Api
dotnet ef migrations add AddPasskeyAuth --project ../LogJammer.Engine
```

Expected: Migration files created in `src/LogJammer.Engine/Data/Migrations/`

- [ ] **Step 8: Verify the build compiles**

Run: `cd src/LogJammer.Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 9: Commit**

```bash
git add src/LogJammer.Api/LogJammer.Api.csproj src/LogJammer.Engine/Data/
git commit -m "feat: add passkey auth EF entities and migration"
```

---

## Task 2: TokenHasher and TokenService Changes

**Files:**
- Create: `src/LogJammer.Api/Auth/TokenHasher.cs`
- Modify: `src/LogJammer.Api/Auth/TokenService.cs`

- [ ] **Step 1: Create TokenHasher utility**

Create `src/LogJammer.Api/Auth/TokenHasher.cs`:

```csharp
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace LogJammer.Api.Auth;

public static class TokenHasher
{
    public static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static string Hash(string token)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
```

- [ ] **Step 2: Update TokenService to track UserId**

Replace the contents of `src/LogJammer.Api/Auth/TokenService.cs` with:

```csharp
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace LogJammer.Api.Auth;

public class TokenService
{
    private readonly record struct TokenEntry(Guid UserId, DateTimeOffset Expiry);
    private readonly ConcurrentDictionary<string, TokenEntry> _tokens = new();

    public string CreateToken(Guid userId)
    {
        CleanExpired();

        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);
        _tokens[token] = new TokenEntry(userId, DateTimeOffset.UtcNow.AddHours(24));
        return token;
    }

    public Guid? ValidateToken(string token)
    {
        if (_tokens.TryGetValue(token, out var entry))
        {
            if (entry.Expiry > DateTimeOffset.UtcNow)
                return entry.UserId;

            _tokens.TryRemove(token, out _);
        }

        return null;
    }

    public void InvalidateUser(Guid userId)
    {
        foreach (var (key, entry) in _tokens)
        {
            if (entry.UserId == userId)
                _tokens.TryRemove(key, out _);
        }
    }

    private void CleanExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, entry) in _tokens)
        {
            if (entry.Expiry <= now)
                _tokens.TryRemove(key, out _);
        }
    }
}
```

- [ ] **Step 3: Verify build**

Run: `cd src/LogJammer.Api && dotnet build`
Expected: Build will fail because AuthMiddleware and AuthController still use old `ValidateToken(string)` returning bool. That's expected — we fix those in the next tasks.

- [ ] **Step 4: Commit**

```bash
git add src/LogJammer.Api/Auth/TokenHasher.cs src/LogJammer.Api/Auth/TokenService.cs
git commit -m "feat: add TokenHasher and update TokenService to track UserId"
```

---

## Task 3: Update AuthMiddleware and AuthSettings

**Files:**
- Modify: `src/LogJammer.Api/Auth/AuthMiddleware.cs`
- Modify: `src/LogJammer.Api/Auth/AuthSettings.cs`

- [ ] **Step 1: Update AuthSettings — remove Password requirement**

Replace `src/LogJammer.Api/Auth/AuthSettings.cs` with:

```csharp
namespace LogJammer.Api.Auth;

public class AuthSettings
{
    public string? ApiKey { get; set; }
}
```

- [ ] **Step 2: Update AuthMiddleware for WebAuthn public paths and UserId**

Replace `src/LogJammer.Api/Auth/AuthMiddleware.cs` with:

```csharp
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

    private static readonly string[] PublicPathPrefixes =
    [
        "/api/invites/",
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

        if (path.Equals("/healthz", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/scalar/", StringComparison.OrdinalIgnoreCase) ||
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

        // Check public path prefixes (invite registration: /api/invites/{token}/register)
        foreach (var prefix in PublicPathPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
                path.EndsWith("/register", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }
        }

        // Check Authorization: Bearer {token}
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var userId = tokenService.ValidateToken(token);
            if (userId.HasValue)
            {
                // Check user is not disabled
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
```

- [ ] **Step 3: Verify build**

Run: `cd src/LogJammer.Api && dotnet build`
Expected: Build may still fail due to AuthController using old LoginRequest/Password — that's fixed in Task 4.

- [ ] **Step 4: Commit**

```bash
git add src/LogJammer.Api/Auth/AuthMiddleware.cs src/LogJammer.Api/Auth/AuthSettings.cs
git commit -m "feat: update AuthMiddleware for WebAuthn public paths and user context"
```

---

## Task 4: WebAuthnService and Rewrite AuthController

**Files:**
- Create: `src/LogJammer.Api/Auth/WebAuthnService.cs`
- Create: `src/LogJammer.Api/Auth/SetupService.cs`
- Create: `src/LogJammer.Api/Dtos/WebAuthnDtos.cs`
- Modify: `src/LogJammer.Api/Controllers/AuthController.cs`
- Modify: `src/LogJammer.Api/Dtos/AuthDtos.cs`
- Modify: `src/LogJammer.Api/Program.cs`
- Modify: `src/LogJammer.Api/appsettings.json`

- [ ] **Step 1: Add Fido2 config to appsettings.json**

Replace `src/LogJammer.Api/appsettings.json` with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=logjammer;Username=logjammer;Password=logjammer"
  },
  "Auth": {
    "ApiKey": "changeme"
  },
  "Fido2": {
    "ServerDomain": "localhost",
    "ServerName": "Log Jammer",
    "Origins": ["https://localhost:5050", "http://localhost:5050", "http://localhost:5173"]
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

- [ ] **Step 2: Create WebAuthnDtos**

Create `src/LogJammer.Api/Dtos/WebAuthnDtos.cs`:

```csharp
namespace LogJammer.Api.Dtos;

public record AuthStatusResponse(bool Initialized);

public record SetupOptionsRequest(string Token, string Username, string DisplayName);
public record SetupRegisterRequest(string Token, object AttestationResponse);

public record LoginOptionsResponse(object Options);
public record LoginCompleteRequest(object AssertionResponse);
public record LoginResponse(string Token, UserInfo User);

public record RegisterOptionsRequest(string? Username, string? DisplayName);

public record UserInfo(Guid Id, string Username, string DisplayName, bool IsAdmin, bool CanInvite);

public record CredentialResponse(Guid Id, string? DeviceInfo, DateTimeOffset CreatedAt);
```

- [ ] **Step 3: Create WebAuthnService**

Create `src/LogJammer.Api/Auth/WebAuthnService.cs`:

```csharp
using Fido2NetLib;
using Fido2NetLib.Objects;
using LogJammer.Engine.Data;
using LogJammer.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LogJammer.Api.Auth;

public class WebAuthnService(IFido2 fido2)
{
    public async Task<CredentialCreateOptions> CreateRegistrationOptionsAsync(
        LogJammerDbContext db,
        string username,
        string displayName,
        Guid? existingUserId = null)
    {
        var fido2User = new Fido2User
        {
            Name = username,
            DisplayName = displayName,
            Id = (existingUserId ?? Guid.NewGuid()).ToByteArray()
        };

        // Get existing credentials for this user (if adding a new passkey)
        var existingCredentials = new List<PublicKeyCredentialDescriptor>();
        if (existingUserId.HasValue)
        {
            var creds = await db.UserCredentials
                .Where(c => c.UserId == existingUserId.Value)
                .Select(c => c.CredentialId)
                .ToListAsync();

            existingCredentials = creds
                .Select(id => new PublicKeyCredentialDescriptor(id))
                .ToList();
        }

        var options = fido2.RequestNewCredential(
            new RequestNewCredentialParams
            {
                User = fido2User,
                ExcludeCredentials = existingCredentials,
                AuthenticatorSelection = new AuthenticatorSelectionCriteria
                {
                    ResidentKey = ResidentKeyRequirement.Preferred,
                    UserVerification = UserVerificationRequirement.Preferred
                },
                AttestationPreference = AttestationConveyancePreference.None
            });

        return options;
    }

    public async Task<UserCredential> CompleteRegistrationAsync(
        AuthenticatorAttestationRawResponse attestationResponse,
        CredentialCreateOptions originalOptions)
    {
        var result = await fido2.MakeNewCredentialAsync(
            new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = originalOptions
            });

        return new UserCredential
        {
            CredentialId = result.Result!.Id,
            PublicKey = result.Result.PublicKey,
            SignCount = result.Result.SignCount
        };
    }

    public async Task<AssertionOptions> CreateLoginOptionsAsync(LogJammerDbContext db)
    {
        var allowedCredentials = await db.UserCredentials
            .Include(c => c.User)
            .Where(c => !c.User.IsDisabled)
            .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
            .ToListAsync();

        var options = fido2.GetAssertionOptions(
            new GetAssertionOptionsParams
            {
                AllowedCredentials = allowedCredentials,
                UserVerification = UserVerificationRequirement.Preferred
            });

        return options;
    }

    public async Task<(UserCredential Credential, uint NewSignCount)> CompleteLoginAsync(
        LogJammerDbContext db,
        AuthenticatorAssertionRawResponse assertionResponse,
        AssertionOptions originalOptions)
    {
        var credentialId = assertionResponse.Id;
        var credential = await db.UserCredentials
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId)
            ?? throw new InvalidOperationException("Credential not found");

        if (credential.User.IsDisabled)
            throw new InvalidOperationException("User is disabled");

        var result = await fido2.MakeAssertionAsync(
            new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = originalOptions,
                StoredPublicKey = credential.PublicKey,
                StoredSignatureCounter = credential.SignCount,
                IsUserHandleOwnerOfCredentialIdCallback = (args, ct) =>
                {
                    var userId = new Guid(args.UserHandle);
                    return Task.FromResult(userId == credential.UserId);
                }
            });

        return (credential, result.Result!.SignCount);
    }
}
```

- [ ] **Step 4: Create SetupService**

Create `src/LogJammer.Api/Auth/SetupService.cs`:

```csharp
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
```

- [ ] **Step 5: Replace AuthDtos**

Replace `src/LogJammer.Api/Dtos/AuthDtos.cs` with:

```csharp
namespace LogJammer.Api.Dtos;

// Moved to WebAuthnDtos.cs — this file is intentionally empty.
// Keeping it to avoid project file churn; delete if preferred.
```

Actually, just delete the file contents and move on — the DTOs are in `WebAuthnDtos.cs` now.

- [ ] **Step 6: Rewrite AuthController**

Replace `src/LogJammer.Api/Controllers/AuthController.cs` with:

```csharp
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

        var credential = await webAuthnService.CompleteRegistrationAsync(attestationResponse, options);

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
        var credential = await webAuthnService.CompleteRegistrationAsync(attestationResponse, options);

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
```

- [ ] **Step 7: Update Program.cs**

Replace `src/LogJammer.Api/Program.cs` with:

```csharp
using Fido2NetLib;
using LogJammer.Api.Auth;
using LogJammer.Api.BackgroundServices;
using LogJammer.Engine;
using LogJammer.Engine.Data;
using LogJammer.Engine.Drain;
using LogJammer.Engine.Processing;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Database
builder.Services.AddDbContext<LogJammerDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Auth
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Auth"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<WebAuthnService>();
builder.Services.AddSingleton<SetupService>();

// Fido2
builder.Services.AddFido2(options =>
{
    options.ServerDomain = builder.Configuration["Fido2:ServerDomain"]!;
    options.ServerName = builder.Configuration["Fido2:ServerName"]!;
    options.Origins = builder.Configuration.GetSection("Fido2:Origins").Get<HashSet<string>>()!;
});

// Session (needed for WebAuthn challenge storage)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

// Engine
builder.Services.AddSingleton(new DrainConfig());
builder.Services.AddSingleton<IngestionPipeline>();
builder.Services.AddScoped<BaselineCalculator>();
builder.Services.AddScoped<PatternStore>();

// Background services
builder.Services.AddHostedService<BaselineRecalculationService>();
builder.Services.AddHostedService<DataRetentionService>();
builder.Services.AddHostedService<ElasticsearchPollingService>();

// API
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevCors", policy =>
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    options.AddPolicy("ExtensionCors", policy =>
        policy.SetIsOriginAllowed(origin => origin.StartsWith("chrome-extension://"))
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LogJammerDbContext>();
    await db.Database.MigrateAsync();
}

// Bootstrap admin setup
var setupService = app.Services.GetRequiredService<SetupService>();
var urls = app.Urls.Any() ? app.Urls.ToArray() : ["http://localhost:5050"];
await setupService.CheckHttpsAsync(urls);
await setupService.CheckAndBootstrapAsync(urls.First());

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseCors("DevCors");
}
else
{
    app.UseCors("ExtensionCors");
}

app.UseSession();
app.UseMiddleware<AuthMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapGet("/healthz", () => "ok");
app.MapFallbackToFile("index.html");

app.Run();
```

- [ ] **Step 8: Verify build**

Run: `cd src/LogJammer.Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 9: Commit**

```bash
git add src/LogJammer.Api/
git commit -m "feat: add WebAuthn auth controller, setup service, and Fido2 registration"
```

---

## Task 5: Invites and Users Controllers

**Files:**
- Create: `src/LogJammer.Api/Dtos/InviteDtos.cs`
- Create: `src/LogJammer.Api/Dtos/UserDtos.cs`
- Create: `src/LogJammer.Api/Controllers/InvitesController.cs`
- Create: `src/LogJammer.Api/Controllers/UsersController.cs`

- [ ] **Step 1: Create InviteDtos**

Create `src/LogJammer.Api/Dtos/InviteDtos.cs`:

```csharp
namespace LogJammer.Api.Dtos;

public record CreateInviteRequest(bool GrantCanInvite);

public record InviteResponse(
    Guid Id,
    bool GrantCanInvite,
    DateTimeOffset ExpiresAt,
    string? UsedByUsername,
    DateTimeOffset? UsedAt,
    DateTimeOffset CreatedAt,
    string? InviteUrl);

public record InviteRegisterOptionsRequest(string Token, string Username, string DisplayName);
```

- [ ] **Step 2: Create UserDtos**

Create `src/LogJammer.Api/Dtos/UserDtos.cs`:

```csharp
namespace LogJammer.Api.Dtos;

public record UserResponse(
    Guid Id,
    string Username,
    string DisplayName,
    bool IsAdmin,
    bool CanInvite,
    bool IsDisabled,
    DateTimeOffset CreatedAt,
    string? InvitedBy);

public record UpdateUserRequest(bool? CanInvite, bool? IsDisabled);
```

- [ ] **Step 3: Create InvitesController**

Create `src/LogJammer.Api/Controllers/InvitesController.cs`:

```csharp
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

        HttpContext.Session.SetString("fido2.invite.options", options.ToJson());
        HttpContext.Session.SetString("fido2.invite.token", token);
        HttpContext.Session.SetString("fido2.invite.username", request.Username);
        HttpContext.Session.SetString("fido2.invite.displayName", request.DisplayName);

        return Ok(options);
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

        var credential = await webAuthnService.CompleteRegistrationAsync(attestationResponse, options);

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
}
```

- [ ] **Step 4: Create UsersController**

Create `src/LogJammer.Api/Controllers/UsersController.cs`:

```csharp
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
```

- [ ] **Step 5: Delete old AuthDtos file**

Delete `src/LogJammer.Api/Dtos/AuthDtos.cs` (its contents are replaced by `WebAuthnDtos.cs`).

- [ ] **Step 6: Verify build**

Run: `cd src/LogJammer.Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 7: Commit**

```bash
git add src/LogJammer.Api/Controllers/ src/LogJammer.Api/Dtos/
git commit -m "feat: add invites and users controllers with DTOs"
```

---

## Task 6: Frontend — Install Dependencies and Add Types

**Files:**
- Modify: `src/frontend/package.json`
- Modify: `src/frontend/src/api/types.ts`

- [ ] **Step 1: Install @simplewebauthn/browser**

Run:
```bash
cd src/frontend && npm install @simplewebauthn/browser
```

- [ ] **Step 2: Add auth/user/invite types**

Append to `src/frontend/src/api/types.ts`:

```typescript
// Auth types
export interface AuthStatusResponse {
  initialized: boolean;
}

export interface UserInfo {
  id: string;
  username: string;
  displayName: string;
  isAdmin: boolean;
  canInvite: boolean;
}

export interface AuthLoginResponse {
  token: string;
  user: UserInfo;
}

export interface CredentialInfo {
  id: string;
  deviceInfo: string | null;
  createdAt: string;
}

// Invite types
export interface InviteResponse {
  id: string;
  grantCanInvite: boolean;
  expiresAt: string;
  usedByUsername: string | null;
  usedAt: string | null;
  createdAt: string;
  inviteUrl: string | null;
}

// User management types
export interface UserResponse {
  id: string;
  username: string;
  displayName: string;
  isAdmin: boolean;
  canInvite: boolean;
  isDisabled: boolean;
  createdAt: string;
  invitedBy: string | null;
}
```

- [ ] **Step 3: Commit**

```bash
git add src/frontend/package.json src/frontend/package-lock.json src/frontend/src/api/types.ts
git commit -m "feat: add @simplewebauthn/browser and auth types"
```

---

## Task 7: Frontend — Rewrite Auth Hooks

**Files:**
- Modify: `src/frontend/src/api/hooks/useAuth.ts`
- Create: `src/frontend/src/api/hooks/useSetup.ts`
- Create: `src/frontend/src/api/hooks/useCredentials.ts`
- Create: `src/frontend/src/api/hooks/useInvites.ts`
- Create: `src/frontend/src/api/hooks/useUsers.ts`

- [ ] **Step 1: Rewrite useAuth hook**

Replace `src/frontend/src/api/hooks/useAuth.ts` with:

```typescript
import { createContext, useContext, useState, useCallback } from 'react';
import type { ReactNode } from 'react';
import { createElement } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { apiGet, apiPost } from '../client';
import { startAuthentication } from '@simplewebauthn/browser';
import type { AuthStatusResponse, AuthLoginResponse, UserInfo } from '../types';

interface AuthContextValue {
  token: string | null;
  user: UserInfo | null;
  isAuthenticated: boolean;
  setAuth: (token: string, user: UserInfo) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [token, setTokenState] = useState<string | null>(() =>
    localStorage.getItem('auth_token')
  );
  const [user, setUser] = useState<UserInfo | null>(() => {
    const stored = localStorage.getItem('auth_user');
    return stored ? JSON.parse(stored) : null;
  });

  const setAuth = useCallback((newToken: string, newUser: UserInfo) => {
    localStorage.setItem('auth_token', newToken);
    localStorage.setItem('auth_user', JSON.stringify(newUser));
    setTokenState(newToken);
    setUser(newUser);
  }, []);

  const logout = useCallback(() => {
    localStorage.removeItem('auth_token');
    localStorage.removeItem('auth_user');
    setTokenState(null);
    setUser(null);
  }, []);

  const value: AuthContextValue = {
    token,
    user,
    isAuthenticated: token !== null,
    setAuth,
    logout,
  };

  return createElement(AuthContext.Provider, { value }, children);
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}

export function useAuthStatus() {
  return useQuery({
    queryKey: ['auth-status'],
    queryFn: () => apiGet<AuthStatusResponse>('/auth/status'),
    staleTime: 30_000,
  });
}

export function usePasskeyLogin() {
  const { setAuth } = useAuth();
  return useMutation({
    mutationFn: async () => {
      const options = await apiPost<PublicKeyCredentialRequestOptionsJSON>(
        '/auth/webauthn/login-options'
      );
      const assertion = await startAuthentication({ optionsJSON: options });
      return apiPost<AuthLoginResponse>('/auth/webauthn/login', assertion);
    },
    onSuccess: (data) => {
      setAuth(data.token, data.user);
    },
  });
}
```

- [ ] **Step 2: Create useSetup hook**

Create `src/frontend/src/api/hooks/useSetup.ts`:

```typescript
import { useMutation } from '@tanstack/react-query';
import { apiPost } from '../client';
import { startRegistration } from '@simplewebauthn/browser';
import { useAuth } from './useAuth';
import type { AuthLoginResponse } from '../types';

interface SetupParams {
  token: string;
  username: string;
  displayName: string;
}

export function useSetupAdmin() {
  const { setAuth } = useAuth();
  return useMutation({
    mutationFn: async ({ token, username, displayName }: SetupParams) => {
      const options = await apiPost<PublicKeyCredentialCreationOptionsJSON>(
        '/auth/setup/options',
        { token, username, displayName }
      );
      const attestation = await startRegistration({ optionsJSON: options });
      return apiPost<AuthLoginResponse>('/auth/setup/register', attestation);
    },
    onSuccess: (data) => {
      setAuth(data.token, data.user);
    },
  });
}
```

- [ ] **Step 3: Create useCredentials hook**

Create `src/frontend/src/api/hooks/useCredentials.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiDelete } from '../client';
import { startRegistration } from '@simplewebauthn/browser';
import type { CredentialInfo, AuthLoginResponse } from '../types';

export function useMyCredentials() {
  return useQuery({
    queryKey: ['my-credentials'],
    queryFn: () => apiGet<CredentialInfo[]>('/users/me/credentials'),
  });
}

export function useAddPasskey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async () => {
      const options = await apiPost<PublicKeyCredentialCreationOptionsJSON>(
        '/auth/webauthn/register-options'
      );
      const attestation = await startRegistration({ optionsJSON: options });
      return apiPost<CredentialInfo>('/auth/webauthn/register', attestation);
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['my-credentials'] });
    },
  });
}

export function useRemovePasskey() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiDelete(`/users/me/credentials/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['my-credentials'] });
    },
  });
}
```

- [ ] **Step 4: Create useInvites hook**

Create `src/frontend/src/api/hooks/useInvites.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '../client';
import { startRegistration } from '@simplewebauthn/browser';
import { useAuth } from './useAuth';
import type { InviteResponse, AuthLoginResponse } from '../types';

export function useInvites() {
  return useQuery({
    queryKey: ['invites'],
    queryFn: () => apiGet<InviteResponse[]>('/invites'),
  });
}

export function useCreateInvite() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (grantCanInvite: boolean) =>
      apiPost<InviteResponse>('/invites', { grantCanInvite }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['invites'] });
    },
  });
}

interface InviteRegisterParams {
  token: string;
  username: string;
  displayName: string;
}

export function useInviteRegister() {
  const { setAuth } = useAuth();
  return useMutation({
    mutationFn: async ({ token, username, displayName }: InviteRegisterParams) => {
      const options = await apiPost<PublicKeyCredentialCreationOptionsJSON>(
        `/invites/${token}/register`,
        { token, username, displayName }
      );
      const attestation = await startRegistration({ optionsJSON: options });
      return apiPost<AuthLoginResponse>(`/invites/${token}/complete`, attestation);
    },
    onSuccess: (data) => {
      setAuth(data.token, data.user);
    },
  });
}
```

- [ ] **Step 5: Create useUsers hook**

Create `src/frontend/src/api/hooks/useUsers.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPut, apiDelete } from '../client';
import type { UserResponse } from '../types';

export function useUsers() {
  return useQuery({
    queryKey: ['users'],
    queryFn: () => apiGet<UserResponse[]>('/users'),
  });
}

export function useUpdateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; canInvite?: boolean; isDisabled?: boolean }) =>
      apiPut(`/users/${id}`, body),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });
}

export function useDeleteUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiDelete(`/users/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['users'] });
    },
  });
}
```

- [ ] **Step 6: Verify frontend build**

Run: `cd src/frontend && npx tsc -b`
Expected: No type errors

- [ ] **Step 7: Commit**

```bash
git add src/frontend/src/api/hooks/
git commit -m "feat: add WebAuthn auth hooks for setup, login, invites, users, credentials"
```

---

## Task 8: Frontend — Setup Page and Login Page

**Files:**
- Create: `src/frontend/src/pages/Setup.tsx`
- Modify: `src/frontend/src/pages/Login.tsx`

- [ ] **Step 1: Create Setup page**

Create `src/frontend/src/pages/Setup.tsx`:

```tsx
import { useState } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import { useSetupAdmin } from '../api/hooks/useSetup';
import { useNavigate } from 'react-router-dom';

export default function Setup() {
  const [token, setToken] = useState(() => {
    const params = new URLSearchParams(window.location.search);
    return params.get('token') ?? '';
  });
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const setup = useSetupAdmin();
  const navigate = useNavigate();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setup.mutate(
      { token, username, displayName },
      { onSuccess: () => void navigate('/dashboard') }
    );
  }

  const canSubmit = token && username && displayName && !setup.isPending;

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '100vh',
        bgcolor: 'background.default',
      }}
    >
      <Card sx={{ width: 420, p: 2 }}>
        <CardContent>
          <Typography
            variant="h5"
            component="h1"
            sx={{
              mb: 1,
              fontFamily: '"Lexend", sans-serif',
              fontWeight: 700,
              letterSpacing: '0.05em',
              color: 'primary.main',
            }}
          >
            Log Jammer
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            Initial Setup
          </Typography>

          <Alert severity="info" sx={{ mb: 3 }}>
            This instance has not been set up yet. Check the application logs
            for the setup token, or paste it below to create the admin account.
          </Alert>

          {setup.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {setup.error instanceof Error ? setup.error.message : 'Setup failed'}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit}>
            <TextField
              label="Setup Token"
              fullWidth
              value={token}
              onChange={(e) => setToken(e.target.value)}
              sx={{ mb: 2 }}
              disabled={setup.isPending}
              helperText="From the application logs"
            />
            <TextField
              label="Username"
              fullWidth
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              sx={{ mb: 2 }}
              disabled={setup.isPending}
              autoFocus={!!token}
            />
            <TextField
              label="Display Name"
              fullWidth
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              sx={{ mb: 2 }}
              disabled={setup.isPending}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={!canSubmit}
            >
              {setup.isPending ? 'Setting up…' : 'Set Up Admin Account'}
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
```

- [ ] **Step 2: Rewrite Login page for passkeys**

Replace `src/frontend/src/pages/Login.tsx` with:

```tsx
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import FingerprintIcon from '@mui/icons-material/Fingerprint';
import { usePasskeyLogin } from '../api/hooks/useAuth';
import { useNavigate } from 'react-router-dom';

export default function Login() {
  const login = usePasskeyLogin();
  const navigate = useNavigate();

  function handleLogin() {
    login.mutate(undefined, {
      onSuccess: () => void navigate('/dashboard'),
    });
  }

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '100vh',
        bgcolor: 'background.default',
      }}
    >
      <Card sx={{ width: 360, p: 2 }}>
        <CardContent>
          <Typography
            variant="h5"
            component="h1"
            sx={{
              mb: 1,
              fontFamily: '"Lexend", sans-serif',
              fontWeight: 700,
              letterSpacing: '0.05em',
              color: 'primary.main',
            }}
          >
            Log Jammer
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Log monitoring & anomaly detection
          </Typography>

          {login.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {login.error instanceof Error ? login.error.message : 'Login failed'}
            </Alert>
          )}

          <Button
            variant="contained"
            fullWidth
            onClick={handleLogin}
            disabled={login.isPending}
            startIcon={<FingerprintIcon />}
            size="large"
          >
            {login.isPending ? 'Authenticating…' : 'Sign in with Passkey'}
          </Button>
        </CardContent>
      </Card>
    </Box>
  );
}
```

- [ ] **Step 3: Verify frontend build**

Run: `cd src/frontend && npx tsc -b`
Expected: No type errors

- [ ] **Step 4: Commit**

```bash
git add src/frontend/src/pages/Setup.tsx src/frontend/src/pages/Login.tsx
git commit -m "feat: add setup page and rewrite login for passkeys"
```

---

## Task 9: Frontend — Register Page, Users Page, Updated Settings

**Files:**
- Create: `src/frontend/src/pages/Register.tsx`
- Create: `src/frontend/src/pages/Users.tsx`
- Modify: `src/frontend/src/pages/Settings.tsx`

- [ ] **Step 1: Create Register page (invite registration)**

Create `src/frontend/src/pages/Register.tsx`:

```tsx
import { useState } from 'react';
import Box from '@mui/material/Box';
import Card from '@mui/material/Card';
import CardContent from '@mui/material/CardContent';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Typography from '@mui/material/Typography';
import Alert from '@mui/material/Alert';
import { useInviteRegister } from '../api/hooks/useInvites';
import { useNavigate, useSearchParams } from 'react-router-dom';

export default function Register() {
  const [searchParams] = useSearchParams();
  const inviteToken = searchParams.get('invite') ?? '';
  const [username, setUsername] = useState('');
  const [displayName, setDisplayName] = useState('');
  const register = useInviteRegister();
  const navigate = useNavigate();

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    register.mutate(
      { token: inviteToken, username, displayName },
      { onSuccess: () => void navigate('/dashboard') }
    );
  }

  if (!inviteToken) {
    return (
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'center', minHeight: '100vh', bgcolor: 'background.default' }}>
        <Card sx={{ width: 400, p: 2 }}>
          <CardContent>
            <Alert severity="error">No invite token provided. You need an invite link to register.</Alert>
          </CardContent>
        </Card>
      </Box>
    );
  }

  const canSubmit = username && displayName && !register.isPending;

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '100vh',
        bgcolor: 'background.default',
      }}
    >
      <Card sx={{ width: 420, p: 2 }}>
        <CardContent>
          <Typography
            variant="h5"
            component="h1"
            sx={{
              mb: 1,
              fontFamily: '"Lexend", sans-serif',
              fontWeight: 700,
              letterSpacing: '0.05em',
              color: 'primary.main',
            }}
          >
            Log Jammer
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Create your account
          </Typography>

          {register.isError && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {register.error instanceof Error ? register.error.message : 'Registration failed'}
            </Alert>
          )}

          <Box component="form" onSubmit={handleSubmit}>
            <TextField
              label="Username"
              fullWidth
              value={username}
              onChange={(e) => setUsername(e.target.value)}
              sx={{ mb: 2 }}
              disabled={register.isPending}
              autoFocus
            />
            <TextField
              label="Display Name"
              fullWidth
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
              sx={{ mb: 2 }}
              disabled={register.isPending}
            />
            <Button
              type="submit"
              variant="contained"
              fullWidth
              disabled={!canSubmit}
            >
              {register.isPending ? 'Registering…' : 'Register with Passkey'}
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
```

- [ ] **Step 2: Create Users page (admin)**

Create `src/frontend/src/pages/Users.tsx`:

```tsx
import { useState } from 'react';
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Table from '@mui/material/Table';
import TableHead from '@mui/material/TableHead';
import TableBody from '@mui/material/TableBody';
import TableRow from '@mui/material/TableRow';
import TableCell from '@mui/material/TableCell';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Switch from '@mui/material/Switch';
import Chip from '@mui/material/Chip';
import Button from '@mui/material/Button';
import Dialog from '@mui/material/Dialog';
import DialogTitle from '@mui/material/DialogTitle';
import DialogContent from '@mui/material/DialogContent';
import DialogActions from '@mui/material/DialogActions';
import Checkbox from '@mui/material/Checkbox';
import FormControlLabel from '@mui/material/FormControlLabel';
import Snackbar from '@mui/material/Snackbar';
import Alert from '@mui/material/Alert';
import DeleteIcon from '@mui/icons-material/Delete';
import PersonAddIcon from '@mui/icons-material/PersonAdd';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import CircularProgress from '@mui/material/CircularProgress';
import { useUsers, useUpdateUser, useDeleteUser } from '../api/hooks/useUsers';
import { useCreateInvite, useInvites } from '../api/hooks/useInvites';
import { useAuth } from '../api/hooks/useAuth';

export default function Users() {
  const { user: currentUser } = useAuth();
  const { data: users, isLoading } = useUsers();
  const { data: invites } = useInvites();
  const updateUser = useUpdateUser();
  const deleteUser = useDeleteUser();
  const createInvite = useCreateInvite();

  const [inviteDialogOpen, setInviteDialogOpen] = useState(false);
  const [grantCanInvite, setGrantCanInvite] = useState(false);
  const [copiedUrl, setCopiedUrl] = useState<string | null>(null);
  const [snackbar, setSnackbar] = useState<string | null>(null);

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', mt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  function handleCreateInvite() {
    createInvite.mutate(grantCanInvite, {
      onSuccess: (data) => {
        setInviteDialogOpen(false);
        setGrantCanInvite(false);
        if (data.inviteUrl) {
          void navigator.clipboard.writeText(data.inviteUrl);
          setCopiedUrl(data.inviteUrl);
          setSnackbar('Invite link copied to clipboard');
        }
      },
    });
  }

  return (
    <Box>
      <Box sx={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', mb: 3 }}>
        <Typography variant="h5" sx={{ fontWeight: 600 }}>
          Users
        </Typography>
        <Button
          variant="contained"
          startIcon={<PersonAddIcon />}
          onClick={() => setInviteDialogOpen(true)}
        >
          Create Invite
        </Button>
      </Box>

      <Paper>
        <Table>
          <TableHead>
            <TableRow>
              <TableCell>User</TableCell>
              <TableCell>Role</TableCell>
              <TableCell>Can Invite</TableCell>
              <TableCell>Status</TableCell>
              <TableCell>Invited By</TableCell>
              <TableCell>Joined</TableCell>
              <TableCell align="right">Actions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {users?.map((u) => (
              <TableRow key={u.id}>
                <TableCell>
                  <Typography variant="body2" sx={{ fontWeight: 500 }}>
                    {u.displayName}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {u.username}
                  </Typography>
                </TableCell>
                <TableCell>
                  {u.isAdmin && <Chip label="Admin" size="small" color="primary" />}
                </TableCell>
                <TableCell>
                  <Switch
                    checked={u.canInvite}
                    onChange={(e) =>
                      updateUser.mutate({ id: u.id, canInvite: e.target.checked })
                    }
                    disabled={u.isAdmin}
                    size="small"
                  />
                </TableCell>
                <TableCell>
                  <Chip
                    label={u.isDisabled ? 'Disabled' : 'Active'}
                    size="small"
                    color={u.isDisabled ? 'error' : 'success'}
                    variant="outlined"
                  />
                </TableCell>
                <TableCell>
                  <Typography variant="body2" color="text.secondary">
                    {u.invitedBy ?? '—'}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Typography variant="body2" color="text.secondary">
                    {new Date(u.createdAt).toLocaleDateString()}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  {!u.isAdmin && (
                    <>
                      <Tooltip title={u.isDisabled ? 'Enable' : 'Disable'}>
                        <Button
                          size="small"
                          onClick={() =>
                            updateUser.mutate({ id: u.id, isDisabled: !u.isDisabled })
                          }
                        >
                          {u.isDisabled ? 'Enable' : 'Disable'}
                        </Button>
                      </Tooltip>
                      <Tooltip title="Delete">
                        <IconButton
                          size="small"
                          color="error"
                          onClick={() => {
                            if (confirm(`Delete user ${u.displayName}?`))
                              deleteUser.mutate(u.id);
                          }}
                        >
                          <DeleteIcon fontSize="small" />
                        </IconButton>
                      </Tooltip>
                    </>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Paper>

      {/* Invite Dialog */}
      <Dialog open={inviteDialogOpen} onClose={() => setInviteDialogOpen(false)}>
        <DialogTitle>Create Invite</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Generate an invite link for a new user. The link expires in 24 hours.
          </Typography>
          <FormControlLabel
            control={
              <Checkbox
                checked={grantCanInvite}
                onChange={(e) => setGrantCanInvite(e.target.checked)}
              />
            }
            label="Allow this user to invite others"
          />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setInviteDialogOpen(false)}>Cancel</Button>
          <Button
            variant="contained"
            onClick={handleCreateInvite}
            disabled={createInvite.isPending}
          >
            {createInvite.isPending ? 'Creating…' : 'Create & Copy Link'}
          </Button>
        </DialogActions>
      </Dialog>

      {/* Copied URL display */}
      {copiedUrl && (
        <Paper sx={{ mt: 2, p: 2, display: 'flex', alignItems: 'center', gap: 1 }}>
          <Typography variant="body2" sx={{ fontFamily: 'monospace', flex: 1, wordBreak: 'break-all' }}>
            {copiedUrl}
          </Typography>
          <IconButton
            size="small"
            onClick={() => {
              void navigator.clipboard.writeText(copiedUrl);
              setSnackbar('Copied!');
            }}
          >
            <ContentCopyIcon fontSize="small" />
          </IconButton>
        </Paper>
      )}

      <Snackbar
        open={!!snackbar}
        autoHideDuration={3000}
        onClose={() => setSnackbar(null)}
      >
        <Alert severity="success" onClose={() => setSnackbar(null)}>
          {snackbar}
        </Alert>
      </Snackbar>
    </Box>
  );
}
```

- [ ] **Step 3: Update Settings page**

Replace `src/frontend/src/pages/Settings.tsx` with:

```tsx
import Box from '@mui/material/Box';
import Paper from '@mui/material/Paper';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Divider from '@mui/material/Divider';
import Alert from '@mui/material/Alert';
import IconButton from '@mui/material/IconButton';
import Tooltip from '@mui/material/Tooltip';
import Table from '@mui/material/Table';
import TableHead from '@mui/material/TableHead';
import TableBody from '@mui/material/TableBody';
import TableRow from '@mui/material/TableRow';
import TableCell from '@mui/material/TableCell';
import DownloadIcon from '@mui/icons-material/Download';
import ExtensionIcon from '@mui/icons-material/Extension';
import ContentCopyIcon from '@mui/icons-material/ContentCopy';
import FingerprintIcon from '@mui/icons-material/Fingerprint';
import DeleteIcon from '@mui/icons-material/Delete';
import AddIcon from '@mui/icons-material/Add';
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { apiGet } from '../api/client';
import { useMyCredentials, useAddPasskey, useRemovePasskey } from '../api/hooks/useCredentials';

const baseUrl = window.location.origin;

const installSteps = [
  'Download the extension zip file using the button below.',
  'Unzip the downloaded file to a folder on your computer.',
  'Open Chrome and navigate to chrome://extensions.',
  'Enable "Developer mode" using the toggle in the top-right corner.',
  'Click "Load unpacked" and select the unzipped folder.',
  'The Log Jammer extension icon will appear in your toolbar.',
  'Click the extension icon, go to the Settings tab, and enter the configuration values shown below.',
];

function CopyableField({ label, value }: { label: string; value: string }) {
  const [copied, setCopied] = useState(false);

  const handleCopy = () => {
    void navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <Box
      sx={{
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        px: 2,
        py: 1,
        bgcolor: 'action.hover',
        borderRadius: 1,
      }}
    >
      <Box>
        <Typography variant="caption" color="text.secondary">
          {label}
        </Typography>
        <Typography
          variant="body2"
          sx={{ fontFamily: 'monospace', fontWeight: 500 }}
        >
          {value}
        </Typography>
      </Box>
      <Tooltip title={copied ? 'Copied!' : 'Copy'}>
        <IconButton size="small" onClick={handleCopy}>
          <ContentCopyIcon fontSize="small" />
        </IconButton>
      </Tooltip>
    </Box>
  );
}

export default function Settings() {
  const { data: apiKeyData } = useQuery({
    queryKey: ['api-key'],
    queryFn: () => apiGet<{ apiKey: string }>('/settings/api-key'),
  });
  const { data: credentials } = useMyCredentials();
  const addPasskey = useAddPasskey();
  const removePasskey = useRemovePasskey();

  return (
    <Box>
      <Typography variant="h5" sx={{ mb: 3, fontWeight: 600 }}>
        Settings
      </Typography>

      {/* Passkeys Section */}
      <Paper sx={{ p: 3, maxWidth: 720, mb: 3 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 2 }}>
          <FingerprintIcon color="primary" />
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            My Passkeys
          </Typography>
        </Box>

        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Manage the passkeys registered to your account. You can add passkeys
          from multiple devices for backup access.
        </Typography>

        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Device</TableCell>
              <TableCell>Registered</TableCell>
              <TableCell align="right" />
            </TableRow>
          </TableHead>
          <TableBody>
            {credentials?.map((cred) => (
              <TableRow key={cred.id}>
                <TableCell>
                  <Typography variant="body2">
                    {cred.deviceInfo || 'Unknown device'}
                  </Typography>
                </TableCell>
                <TableCell>
                  <Typography variant="body2" color="text.secondary">
                    {new Date(cred.createdAt).toLocaleDateString()}
                  </Typography>
                </TableCell>
                <TableCell align="right">
                  <Tooltip
                    title={
                      (credentials?.length ?? 0) <= 1
                        ? 'Cannot remove last passkey'
                        : 'Remove'
                    }
                  >
                    <span>
                      <IconButton
                        size="small"
                        color="error"
                        disabled={(credentials?.length ?? 0) <= 1}
                        onClick={() => removePasskey.mutate(cred.id)}
                      >
                        <DeleteIcon fontSize="small" />
                      </IconButton>
                    </span>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        <Button
          variant="outlined"
          startIcon={<AddIcon />}
          onClick={() => addPasskey.mutate()}
          disabled={addPasskey.isPending}
          sx={{ mt: 2 }}
        >
          {addPasskey.isPending ? 'Adding…' : 'Add Passkey'}
        </Button>

        {addPasskey.isError && (
          <Alert severity="error" sx={{ mt: 2 }}>
            {addPasskey.error instanceof Error ? addPasskey.error.message : 'Failed to add passkey'}
          </Alert>
        )}
      </Paper>

      {/* Extension Section */}
      <Paper sx={{ p: 3, maxWidth: 720 }}>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1.5, mb: 2 }}>
          <ExtensionIcon color="primary" />
          <Typography variant="h6" sx={{ fontWeight: 600 }}>
            Chrome Extension
          </Typography>
        </Box>

        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          The Log Jammer Chrome extension captures Kibana queries and forwards
          them to your Log Jammer instance for pattern analysis. It runs as an
          unpacked extension installed in developer mode.
        </Typography>

        <Button
          variant="contained"
          startIcon={<DownloadIcon />}
          href="/downloads/log-jammer-extension.zip"
          download
          sx={{ mb: 3 }}
        >
          Download Extension
        </Button>

        <Divider sx={{ mb: 2 }} />

        <Typography variant="subtitle2" sx={{ mb: 1.5, fontWeight: 600 }}>
          Installation steps
        </Typography>

        <Box component="ol" sx={{ pl: 2.5, m: 0, mb: 3 }}>
          {installSteps.map((step, i) => (
            <Typography
              key={i}
              component="li"
              variant="body2"
              color="text.secondary"
              sx={{ mb: 1 }}
            >
              {step}
            </Typography>
          ))}
        </Box>

        <Divider sx={{ mb: 2 }} />

        <Typography variant="subtitle2" sx={{ mb: 1.5, fontWeight: 600 }}>
          Extension configuration
        </Typography>

        <Alert severity="info" sx={{ mb: 2 }}>
          Enter these values in the extension's Settings tab after installation.
        </Alert>

        <Box sx={{ display: 'flex', flexDirection: 'column', gap: 1.5 }}>
          <CopyableField label="Log Jammer URL" value={baseUrl} />
          <CopyableField
            label="API Key"
            value={apiKeyData?.apiKey || '—'}
          />
        </Box>
      </Paper>
    </Box>
  );
}
```

- [ ] **Step 4: Verify frontend build**

Run: `cd src/frontend && npx tsc -b`
Expected: No type errors

- [ ] **Step 5: Commit**

```bash
git add src/frontend/src/pages/
git commit -m "feat: add register page, users page, and update settings with passkey management"
```

---

## Task 10: Frontend — Update App Routing and Sidebar

**Files:**
- Modify: `src/frontend/src/App.tsx`
- Modify: `src/frontend/src/components/Sidebar.tsx`

- [ ] **Step 1: Update App.tsx with setup/register routes and initialization check**

Replace `src/frontend/src/App.tsx` with:

```tsx
import { Navigate, Route, Routes } from 'react-router-dom';
import { useAuth, useAuthStatus } from './api/hooks/useAuth';
import Layout from './components/Layout';
import Login from './pages/Login';
import Setup from './pages/Setup';
import Register from './pages/Register';
import Dashboard from './pages/Dashboard';
import DataSources from './pages/DataSources';
import Patterns from './pages/Patterns';
import PatternDetail from './pages/PatternDetail';
import Settings from './pages/Settings';
import Users from './pages/Users';
import CircularProgress from '@mui/material/CircularProgress';
import Box from '@mui/material/Box';
import type { ReactNode } from 'react';

interface ProtectedRouteProps {
  children: ReactNode;
}

function ProtectedRoute({ children }: ProtectedRouteProps) {
  const { isAuthenticated } = useAuth();
  if (!isAuthenticated) {
    return <Navigate to="/" replace />;
  }
  return <>{children}</>;
}

function AdminRoute({ children }: ProtectedRouteProps) {
  const { isAuthenticated, user } = useAuth();
  if (!isAuthenticated) return <Navigate to="/" replace />;
  if (!user?.isAdmin) return <Navigate to="/dashboard" replace />;
  return <>{children}</>;
}

export default function App() {
  const { isAuthenticated } = useAuth();
  const { data: status, isLoading } = useAuthStatus();

  if (isLoading) {
    return (
      <Box sx={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '100vh' }}>
        <CircularProgress />
      </Box>
    );
  }

  const initialized = status?.initialized ?? true;

  return (
    <Routes>
      {/* Public routes */}
      <Route path="/register" element={<Register />} />
      <Route
        path="/setup"
        element={initialized ? <Navigate to="/" replace /> : <Setup />}
      />
      <Route
        path="/"
        element={
          !initialized ? (
            <Navigate to="/setup" replace />
          ) : isAuthenticated ? (
            <Navigate to="/dashboard" replace />
          ) : (
            <Login />
          )
        }
      />

      {/* Protected routes */}
      <Route
        element={
          <ProtectedRoute>
            <Layout />
          </ProtectedRoute>
        }
      >
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/patterns" element={<Patterns />} />
        <Route path="/data-sources" element={<DataSources />} />
        <Route path="/patterns/:id" element={<PatternDetail />} />
        <Route path="/settings" element={<Settings />} />
        <Route
          path="/users"
          element={
            <AdminRoute>
              <Users />
            </AdminRoute>
          }
        />
      </Route>
      <Route path="*" element={<Navigate to="/dashboard" replace />} />
    </Routes>
  );
}
```

- [ ] **Step 2: Update Sidebar with Users nav item (admin only)**

Replace `src/frontend/src/components/Sidebar.tsx` with:

```tsx
import Drawer from '@mui/material/Drawer';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Toolbar from '@mui/material/Toolbar';
import DashboardIcon from '@mui/icons-material/Dashboard';
import ListAltIcon from '@mui/icons-material/ListAlt';
import StorageIcon from '@mui/icons-material/Storage';
import SettingsIcon from '@mui/icons-material/Settings';
import PeopleIcon from '@mui/icons-material/People';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../api/hooks/useAuth';

const DRAWER_WIDTH = 240;

interface NavItem {
  label: string;
  path: string;
  icon: React.ReactNode;
  adminOnly?: boolean;
}

const navItems: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: <DashboardIcon /> },
  { label: 'Patterns', path: '/patterns', icon: <ListAltIcon /> },
  { label: 'Data Sources', path: '/data-sources', icon: <StorageIcon /> },
  { label: 'Users', path: '/users', icon: <PeopleIcon />, adminOnly: true },
  { label: 'Settings', path: '/settings', icon: <SettingsIcon /> },
];

export default function Sidebar() {
  const navigate = useNavigate();
  const location = useLocation();
  const { user } = useAuth();

  const visibleItems = navItems.filter(
    (item) => !item.adminOnly || user?.isAdmin
  );

  return (
    <Drawer
      variant="permanent"
      sx={{
        width: DRAWER_WIDTH,
        flexShrink: 0,
        '& .MuiDrawer-paper': {
          width: DRAWER_WIDTH,
          boxSizing: 'border-box',
        },
      }}
    >
      <Toolbar />
      <List sx={{ pt: 2 }}>
        {visibleItems.map(({ label, path, icon }) => {
          const isActive = location.pathname === path || location.pathname.startsWith(path + '/');
          return (
            <ListItem key={path} disablePadding>
              <ListItemButton
                onClick={() => void navigate(path)}
                selected={isActive}
                sx={{
                  mx: 1,
                  borderRadius: 1,
                  mb: 0.5,
                  '&.Mui-selected': {
                    backgroundColor: 'action.selected',
                    borderLeft: '3px solid',
                    borderColor: 'primary.main',
                    '& .MuiListItemIcon-root': { color: 'primary.main' },
                    '& .MuiListItemText-primary': { color: 'primary.main' },
                  },
                }}
              >
                <ListItemIcon
                  sx={{
                    minWidth: 40,
                    color: isActive ? 'primary.main' : 'text.secondary',
                  }}
                >
                  {icon}
                </ListItemIcon>
                <ListItemText
                  primary={label}
                  slotProps={{
                    primary: {
                      sx: {
                        fontSize: '0.875rem',
                        fontWeight: isActive ? 600 : 400,
                        color: isActive ? 'primary.main' : 'text.secondary',
                      },
                    },
                  }}
                />
              </ListItemButton>
            </ListItem>
          );
        })}
      </List>
    </Drawer>
  );
}
```

- [ ] **Step 3: Verify frontend build**

Run: `cd src/frontend && npx tsc -b`
Expected: No type errors

- [ ] **Step 4: Verify full backend build**

Run: `cd src/LogJammer.Api && dotnet build`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/frontend/src/App.tsx src/frontend/src/components/Sidebar.tsx
git commit -m "feat: add setup/register/users routes and admin sidebar nav"
```

---

## Task 11: Remove Old Password Auth

**Files:**
- Delete: `src/LogJammer.Api/Dtos/AuthDtos.cs`
- Modify: `src/LogJammer.Api/appsettings.json` (already done in Task 4)

- [ ] **Step 1: Delete AuthDtos.cs if still present**

```bash
rm -f src/LogJammer.Api/Dtos/AuthDtos.cs
```

- [ ] **Step 2: Verify full build (backend + frontend)**

Run:
```bash
cd src/LogJammer.Api && dotnet build
cd ../frontend && npx tsc -b
```

Expected: Both builds succeed

- [ ] **Step 3: Commit**

```bash
git add -A
git commit -m "chore: remove old password auth DTOs"
```

---

## Task 12: End-to-End Smoke Test

No new files. Manual verification.

- [ ] **Step 1: Start the application**

```bash
cd src/LogJammer.Api && dotnet run
```

Expected: Application starts. Look for the prominent setup message in the logs:
```
════════════════════════════════════════════════════════════
  LOG JAMMER — SETUP REQUIRED
  ...
════════════════════════════════════════════════════════════
```

- [ ] **Step 2: Verify frontend shows setup screen**

Open `http://localhost:5173` (or wherever the frontend dev server runs).
Expected: Redirects to `/setup` with the "Log Jammer has not been set up yet" message.

- [ ] **Step 3: Complete admin setup**

Paste the setup token from the logs, enter a username and display name, click "Set Up Admin Account".
Expected: Browser prompts for passkey creation (fingerprint/face). After success, redirected to dashboard.

- [ ] **Step 4: Verify login works**

Log out, then click "Sign in with Passkey".
Expected: Browser prompts for passkey, then redirected to dashboard.

- [ ] **Step 5: Verify Users page and invite flow**

Navigate to Users page. Click "Create Invite". Copy the invite link. Open in incognito window.
Expected: Registration page loads, allows creating a new account with passkey.

- [ ] **Step 6: Verify Settings page**

Navigate to Settings. Check that API key displays, passkeys list shows, and "Add Passkey" works.

- [ ] **Step 7: Commit any fixes**

If any issues were found and fixed during testing, commit them:
```bash
git add -A
git commit -m "fix: address issues found during passkey auth smoke test"
```
