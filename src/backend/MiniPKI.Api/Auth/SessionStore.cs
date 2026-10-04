using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace MiniPKI.Api.Auth;

/// <summary>
/// In-memory session store with expiration.
/// </summary>
public class SessionStore
{
    private readonly ConcurrentDictionary<string, DateTime> _sessions = new();
    private readonly int _timeoutMinutes;

    public SessionStore(IConfiguration config)
    {
        _timeoutMinutes = config.GetValue<int>("Auth:SessionTimeoutMinutes", 60);
    }

    /// <summary>
    /// Creates a new session and returns the session token.
    /// </summary>
    public string CreateSession()
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _sessions[token] = DateTime.UtcNow.AddMinutes(_timeoutMinutes);
        return token;
    }

    /// <summary>
    /// Validates a session token. Returns false if invalid or expired.
    /// </summary>
    public bool IsValid(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        if (!_sessions.TryGetValue(token, out var expiry)) return false;
        if (DateTime.UtcNow > expiry)
        {
            _sessions.TryRemove(token, out _);
            return false;
        }
        return true;
    }

    /// <summary>
    /// Removes a session (logout).
    /// </summary>
    public void RemoveSession(string? token)
    {
        if (!string.IsNullOrEmpty(token))
            _sessions.TryRemove(token, out _);
    }
}
