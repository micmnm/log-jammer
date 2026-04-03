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
