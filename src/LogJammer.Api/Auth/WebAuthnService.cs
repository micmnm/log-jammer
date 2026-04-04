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
                AuthenticatorSelection = new AuthenticatorSelection
                {
                    ResidentKey = ResidentKeyRequirement.Preferred,
                    UserVerification = UserVerificationRequirement.Preferred
                },
                AttestationPreference = AttestationConveyancePreference.None
            });

        return options;
    }

    public async Task<UserCredential> CompleteRegistrationAsync(
        LogJammerDbContext db,
        AuthenticatorAttestationRawResponse attestationResponse,
        CredentialCreateOptions originalOptions)
    {
        IsCredentialIdUniqueToUserAsyncDelegate callback = async (args, cancellationToken) =>
        {
            var exists = await db.UserCredentials
                .AnyAsync(c => c.CredentialId == args.CredentialId, cancellationToken);
            return !exists;
        };

        var credential = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = attestationResponse,
            OriginalOptions = originalOptions,
            IsCredentialIdUniqueToUserCallback = callback
        });

        return new UserCredential
        {
            CredentialId = credential.Id,
            PublicKey = credential.PublicKey,
            SignCount = credential.SignCount
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
        var credentialIdBytes = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(assertionResponse.Id);
        var credential = await db.UserCredentials
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.CredentialId == credentialIdBytes)
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
                    // We already looked up the credential by ID above and verified
                    // the user is not disabled. Confirm ownership.
                    return Task.FromResult(credential.CredentialId.AsSpan().SequenceEqual(args.CredentialId));
                }
            });

        return (credential, result.SignCount);
    }
}
