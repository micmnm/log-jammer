# Passkey Authentication with Invite-Only Registration

Replace password-only auth with WebAuthn passkeys. Limit access via invite-only registration with a cascading permission model. Bootstrap the first admin user on first startup.

## Current State

- Single shared password in `appsettings.json` (`Auth.Password`)
- Custom token-based sessions via `TokenService` (in-memory, 24h expiry)
- API key auth for browser extension (`Auth.ApiKey`, `X-Api-Key` header)
- No user model, no database-backed accounts
- Frontend stores bearer token in `localStorage`

## Goals

1. Replace password login with passkey (WebAuthn) authentication
2. Introduce a user model with per-user credentials
3. Restrict registration to invite-only
4. Bootstrap the first admin via a one-time setup flow on first startup
5. Keep API key auth unchanged for the browser extension
6. Allow admin to manage users (revoke access, control invite permissions)

## Non-Goals

- Multi-tenant support
- OAuth / social login
- Per-user API keys (shared key stays as-is)
- Email-based flows (no email verification, no password reset)

---

## 1. Database Schema

Three new tables added to `LogJammerDbContext`.

### Users

| Column | Type | Notes |
|--------|------|-------|
| Id | Guid | PK, auto-generated |
| Username | string | Required, unique, from WebAuthn registration |
| DisplayName | string | Required, from WebAuthn registration |
| IsAdmin | bool | Default false. First user = true |
| CanInvite | bool | Default false. First user = true. Controlled by invite toggle and admin |
| IsDisabled | bool | Default false. Disabled users cannot authenticate |
| CreatedAt | DateTimeOffset | Set on creation |

### Credentials

| Column | Type | Notes |
|--------|------|-------|
| Id | Guid | PK, auto-generated |
| UserId | Guid | FK → Users, required |
| CredentialId | byte[] | WebAuthn credential ID, unique |
| PublicKey | byte[] | WebAuthn public key |
| SignCount | uint | Replay attack counter |
| CreatedAt | DateTimeOffset | Set on creation |
| DeviceInfo | string | Nullable, user-agent or authenticator info for display |

A user can have multiple credentials (multiple passkeys on different devices).

### Invites

| Column | Type | Notes |
|--------|------|-------|
| Id | Guid | PK, auto-generated |
| TokenHash | string | SHA-256 hash of the raw token, unique |
| CreatedByUserId | Guid | FK → Users |
| GrantCanInvite | bool | Whether the registering user gets CanInvite |
| ExpiresAt | DateTimeOffset | Default 24h from creation |
| UsedByUserId | Guid? | FK → Users, nullable. Set on use |
| UsedAt | DateTimeOffset? | Set on use |

### SetupTokens

| Column | Type | Notes |
|--------|------|-------|
| Id | Guid | PK, auto-generated |
| TokenHash | string | SHA-256 hash of the raw token, unique |
| CreatedAt | DateTimeOffset | Set on creation |
| ExpiresAt | DateTimeOffset | 1 hour from creation |
| UsedAt | DateTimeOffset? | Set on use |

Separate table for setup tokens to keep the bootstrap flow independent. Only one valid (unused, unexpired) setup token should exist at a time.

---

## 2. Bootstrap Flow (First Startup)

### Backend

On application startup, after database migration:

1. Check if `Users` table has any rows
2. If empty, check if a valid (unused, unexpired) setup token exists in `SetupTokens`
3. If no valid token exists, generate one:
   - 32 cryptographically random bytes → base64url encode → raw token
   - Store SHA-256 hash in `SetupTokens` with 1-hour expiry
4. Log the setup URL prominently:

```
════════════════════════════════════════════════════════════
  LOG JAMMER — SETUP REQUIRED

  No admin account found. Register the first admin at:

  {baseUrl}/setup?token=<raw-token>

  This link expires in 1 hour.
════════════════════════════════════════════════════════════
```

Use `LogLevel.Critical` or equivalent to ensure the message is not buried. Log it via `ILogger` with a dedicated category (`LogJammer.Setup`) so it stands out.

**Race condition handling:** The setup token insert uses a DB-level check — if `Users` table is not empty at insert time, skip. This handles multiple instances starting simultaneously.

### Frontend

1. On app load, call `GET /api/auth/status`
2. If `{ initialized: false }`:
   - Do **not** show the login page
   - Show a dedicated setup screen with:
     - Clear message: "Log Jammer has not been set up yet"
     - Explanation: "Check the application logs for a setup link, or enter the setup token below"
     - Input field for the setup token
     - "Set Up Admin Account" button
3. On token submission:
   - Call `POST /api/auth/setup/options` with token
   - Server validates token, returns WebAuthn registration options
   - Browser prompts for passkey creation (fingerprint/face/security key)
   - Frontend sends attestation to `POST /api/auth/setup/register`
   - Server creates the admin user (`IsAdmin: true`, `CanInvite: true`), stores credential, marks setup token as used
   - Frontend receives bearer token, redirects to dashboard

---

## 3. Authentication Flow

### Login (WebAuthn Assertion)

1. User visits the app, frontend shows login page (if initialized)
2. User clicks "Sign in with passkey"
3. Frontend calls `POST /api/auth/webauthn/login-options` → server returns challenge + allowed credential IDs
4. Browser prompts user for passkey (fingerprint/face/security key)
5. Frontend sends assertion to `POST /api/auth/webauthn/login`
6. Server validates assertion via Fido2NetLib, checks `SignCount`, verifies user is not disabled
7. Server issues bearer token via existing `TokenService` (24h expiry, in-memory store)
8. Frontend stores token in `localStorage`, proceeds to dashboard

### Token Validation

Reuse existing `AuthMiddleware` and `TokenService`. The only change: `TokenService` now associates tokens with a `UserId` so we can invalidate all tokens for a disabled user.

### API Key Auth

Unchanged. `X-Api-Key` header continues to work for the browser extension. The `Auth.ApiKey` config stays in `appsettings.json`.

---

## 4. Invite System

### Creating an Invite

- **Who can create:** Users with `CanInvite = true`
- **Endpoint:** `POST /api/invites`
- **Request body:** `{ grantCanInvite: bool }` — the "Can invite others" toggle
- **Process:**
  1. Generate 32 random bytes → base64url encode → raw token
  2. Store SHA-256 hash in `Invites` table with `CreatedByUserId`, `GrantCanInvite`, `ExpiresAt` (24h)
  3. Return the full invite URL: `{baseUrl}/register?invite=<raw-token>`

### Using an Invite

1. Recipient opens the invite link
2. Frontend calls `POST /api/invites/{token}/register` with the raw token
3. Server hashes the token, looks up the invite, validates (exists, not expired, not used)
4. Server returns WebAuthn registration options
5. Browser prompts for passkey creation — user enters a username and display name
6. Frontend sends attestation back
7. Server creates user (`CanInvite` = invite's `GrantCanInvite`), stores credential, marks invite as used
8. Server issues bearer token, frontend redirects to dashboard

### Viewing Invites

- Regular users see their own invites: `GET /api/invites`
- Admin sees all invites: `GET /api/invites` (filtered server-side by role)

---

## 5. User Management (Admin Only)

### User List

- **Endpoint:** `GET /api/users`
- **Response:** All users with: Id, Username, DisplayName, IsAdmin, CanInvite, IsDisabled, CreatedAt, invited by (from Invites table join)

### Admin Actions

- **Toggle CanInvite:** `PUT /api/users/{id}` with `{ canInvite: bool }` — controls invite chain propagation
- **Disable user:** `PUT /api/users/{id}` with `{ isDisabled: true }` — soft disable, invalidates all active tokens for that user immediately
- **Enable user:** `PUT /api/users/{id}` with `{ isDisabled: false }`
- **Delete user:** `DELETE /api/users/{id}` — hard delete, cascades to credentials and invites. Admin cannot delete themselves.

### Admin UI

- Accessible from navigation (admin only)
- Table of users with status indicators
- Action buttons per user row

---

## 6. Settings Page (All Users)

### API Key Section

- Displays the shared API key from config (read via endpoint, not hardcoded)
- Copy-to-clipboard button
- Brief explanation: "Use this key with the Log Jammer browser extension"
- **Endpoint:** `GET /api/settings/api-key` — returns the API key for authenticated users

### My Passkeys Section

- Lists all registered passkeys for the current user
- Each entry shows: device info (if available), registration date
- **Add passkey:** button triggers WebAuthn registration ceremony for an additional credential
- **Remove passkey:** button per entry, disabled if it's the last remaining passkey (must keep at least one)
- **Endpoints:**
  - `GET /api/users/me/credentials` — list own passkeys
  - `POST /api/auth/webauthn/register-options` + `POST /api/auth/webauthn/register` — add passkey
  - `DELETE /api/users/me/credentials/{id}` — remove passkey (server enforces min 1)

---

## 7. HTTPS Detection

WebAuthn requires a secure context (HTTPS or localhost). On startup:

1. Check the configured URLs / listen addresses
2. If any non-localhost address is HTTP (not HTTPS), log a warning:

```
════════════════════════════════════════════════════════════
  WARNING: WebAuthn requires HTTPS

  Passkey authentication will not work over plain HTTP.
  Configure HTTPS or use a reverse proxy with TLS termination.
════════════════════════════════════════════════════════════
```

Use the same prominent logging style as the setup message. Use `LogLevel.Warning` with category `LogJammer.Setup`.

---

## 8. API Endpoints Summary

### Public (no auth required)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/auth/status` | Returns `{ initialized: bool }` |
| POST | `/api/auth/setup/options` | Validate setup token, return WebAuthn registration options |
| POST | `/api/auth/setup/register` | Complete admin WebAuthn registration |
| POST | `/api/auth/webauthn/login-options` | Get WebAuthn login challenge |
| POST | `/api/auth/webauthn/login` | Validate assertion, return bearer token |
| POST | `/api/invites/{token}/register` | Validate invite + WebAuthn registration |

### Authenticated (bearer token required)

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/auth/webauthn/register-options` | Get WebAuthn registration options (add passkey) |
| POST | `/api/auth/webauthn/register` | Complete passkey registration |
| GET | `/api/users/me/credentials` | List own passkeys |
| DELETE | `/api/users/me/credentials/{id}` | Remove a passkey (min 1 enforced) |
| POST | `/api/invites` | Create invite (requires CanInvite) |
| GET | `/api/invites` | List invites (own, or all for admin) |
| GET | `/api/settings/api-key` | Get API key for extension |

### Admin Only (bearer token + IsAdmin required)

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/api/users` | List all users |
| PUT | `/api/users/{id}` | Update user (CanInvite, IsDisabled) |
| DELETE | `/api/users/{id}` | Delete user |

---

## 9. What Gets Removed

- `POST /api/auth/login` — password login endpoint
- `Auth.Password` in `appsettings.json` — no longer needed
- `LoginRequest` / `LoginResponse` DTOs — replaced by WebAuthn flow
- Password validation logic in `AuthController`

`Auth.ApiKey` stays. `TokenService` stays (modified to track UserId). `AuthMiddleware` stays (modified to also check user disabled status).

---

## 10. Libraries

### Backend

- **Fido2NetLib** (`Fido2NetLib`, `Fido2NetLib.AspNet`) — WebAuthn server-side implementation. Handles attestation/assertion ceremonies, credential storage format, and all FIDO2 protocol details.
- No other new dependencies.

### Frontend

- **@simplewebauthn/browser** — Thin wrapper around the browser's `navigator.credentials` API. Handles encoding/decoding of WebAuthn requests/responses.
- No other new dependencies.

---

## 11. Migration Path

This is a breaking change — existing sessions (bearer tokens) will be invalidated since there are no users to associate them with.

1. Run EF migration to create new tables (Users, Credentials, Invites, SetupTokens)
2. Remove `Auth.Password` from config (or ignore it)
3. On first startup after migration, app enters setup mode
4. Admin registers via setup flow
5. Admin creates invites for other team members

No data migration needed since there are no existing user records.
