namespace LogJammer.Api.Dtos;

public record AuthStatusResponse(bool Initialized);

public record SetupOptionsRequest(string Token, string Username, string DisplayName);

public record LoginResponse(string Token, UserInfo User);

public record UserInfo(Guid Id, string Username, string DisplayName, bool IsAdmin, bool CanInvite);

public record CredentialResponse(Guid Id, string? DeviceInfo, DateTimeOffset CreatedAt);
