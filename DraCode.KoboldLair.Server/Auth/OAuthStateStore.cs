using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace DraCode.KoboldLair.Server.Auth;

/// <summary>
/// In-memory store for short-lived, single-use OAuth <c>state</c> values guarding the GitHub
/// federation callback against login-CSRF (FEATURE-019 D13 / TASK-033). Dedicated store rather
/// than reusing the refresh-token store — different lifetime and concern. Single-process daemon,
/// so losing state across a restart mid-login is acceptable (login is seconds-scale).
/// </summary>
public sealed class OAuthStateStore : IDisposable
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);
    private readonly ConcurrentDictionary<string, DateTime> _states = new();
    private readonly Timer _cleanup;

    public OAuthStateStore()
    {
        _cleanup = new Timer(_ => Sweep(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    /// <summary>Mints, stores, and returns a fresh cryptographically-random state token.</summary>
    public string Issue()
    {
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        _states[state] = DateTime.UtcNow.Add(Ttl);
        return state;
    }

    /// <summary>
    /// Validates and <em>consumes</em> a state token: true only if it exists and hasn't expired.
    /// Single-use — a successful (or expired) lookup removes it.
    /// </summary>
    public bool TryConsume(string? state)
    {
        if (string.IsNullOrEmpty(state)) return false;
        if (!_states.TryRemove(state, out var expiresAt)) return false;
        return expiresAt >= DateTime.UtcNow;
    }

    private void Sweep()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _states)
            if (kvp.Value < now)
                _states.TryRemove(kvp.Key, out _);
    }

    public void Dispose() => _cleanup.Dispose();
}
