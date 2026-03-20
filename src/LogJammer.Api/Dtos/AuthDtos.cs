namespace LogJammer.Api.Dtos;

public record LoginRequest(string Password);
public record LoginResponse(string Token);
