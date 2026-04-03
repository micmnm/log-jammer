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
