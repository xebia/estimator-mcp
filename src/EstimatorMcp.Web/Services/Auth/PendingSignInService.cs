using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace EstimatorMcp.Web.Services.Auth;

/// <summary>
/// Holds one-time sign-in tokens that bridge the gap between the Interactive Server
/// verify component (which can't set cookies) and the /account/do-signin endpoint
/// (which sets the session cookie in a regular HTTP request).
/// Tokens expire after 5 minutes and are consumed exactly once.
/// </summary>
public class PendingSignInService
{
    private readonly ConcurrentDictionary<string, Entry> _pending = new();

    public string Create(string email, string? returnUrl)
    {
        CleanExpired();
        var id = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace("+", "-").Replace("/", "_").TrimEnd('=');
        _pending[id] = new Entry(email, returnUrl, DateTimeOffset.UtcNow.AddMinutes(5));
        return id;
    }

    public bool TryConsume(string id, out string email, out string? returnUrl)
    {
        if (_pending.TryRemove(id, out var entry) && entry.ExpiresAt > DateTimeOffset.UtcNow)
        {
            email = entry.Email;
            returnUrl = entry.ReturnUrl;
            return true;
        }
        email = "";
        returnUrl = null;
        return false;
    }

    private void CleanExpired()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var key in _pending.Keys.ToList())
        {
            if (_pending.TryGetValue(key, out var e) && e.ExpiresAt <= now)
                _pending.TryRemove(key, out _);
        }
    }

    private sealed record Entry(string Email, string? ReturnUrl, DateTimeOffset ExpiresAt);
}
