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
