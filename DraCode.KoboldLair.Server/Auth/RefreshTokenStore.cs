using System.Collections.Concurrent;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// In-memory store for active refresh tokens with expiry tracking.
/// </summary>
public class RefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenEntry> _tokens = new();
    private readonly Timer _cleanupTimer;

    public RefreshTokenStore()
    {
        // Cleanup expired tokens every 5 minutes
        _cleanupTimer = new Timer(CleanupExpired, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void Store(string refreshToken, Guid userId, string username, List<string> roles, DateTime expiresAt)
    {
        _tokens[refreshToken] = new RefreshTokenEntry
        {
            UserId = userId,
            Username = username,
            Roles = roles,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };
    }

    public RefreshTokenEntry? Validate(string refreshToken)
    {
        if (!_tokens.TryGetValue(refreshToken, out var entry))
            return null;

        if (entry.IsRevoked || entry.ExpiresAt < DateTime.UtcNow)
        {
            _tokens.TryRemove(refreshToken, out _);
            return null;
        }

        return entry;
    }

    public bool Revoke(string refreshToken)
    {
        if (_tokens.TryGetValue(refreshToken, out var entry))
        {
            entry.IsRevoked = true;
            return true;
        }
        return false;
    }

    public void RevokeAllForUser(Guid userId)
    {
        var userTokens = _tokens.Where(kvp => kvp.Value.UserId == userId).Select(kvp => kvp.Key).ToList();
        foreach (var token in userTokens)
        {
            _tokens.TryRemove(token, out _);
        }
    }

    private void CleanupExpired(object? state)
    {
        var expired = _tokens.Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow || kvp.Value.IsRevoked)
            .Select(kvp => kvp.Key).ToList();

        foreach (var token in expired)
        {
            _tokens.TryRemove(token, out _);
        }
    }
}

public class RefreshTokenEntry
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = [];
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsRevoked { get; set; }
}
