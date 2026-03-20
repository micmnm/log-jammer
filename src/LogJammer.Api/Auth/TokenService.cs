using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace LogJammer.Api.Auth;

public class TokenService
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _tokens = new();

    public string CreateToken()
    {
        CleanExpired();

        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);
        _tokens[token] = DateTimeOffset.UtcNow.AddHours(24);
        return token;
    }

    public bool ValidateToken(string token)
    {
        if (_tokens.TryGetValue(token, out var expiry))
        {
            if (expiry > DateTimeOffset.UtcNow)
                return true;

            _tokens.TryRemove(token, out _);
        }

        return false;
    }

    private void CleanExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, expiry) in _tokens)
        {
            if (expiry <= now)
                _tokens.TryRemove(key, out _);
        }
    }
}
