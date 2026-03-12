namespace EstimatorMcp.Web.Services.Auth;

/// <summary>
/// Singleton in-memory store that holds newly generated tokens for one-time display.
/// Entries expire after 5 minutes.
/// </summary>
public class TokenDisplayService
{
    private readonly Dictionary<Guid, (string RawToken, string Email, DateTime Expiry)> _pending = new();

    public Guid Store(string rawToken, string email)
    {
        var id = Guid.NewGuid();
        _pending[id] = (rawToken, email, DateTime.UtcNow.AddMinutes(5));
        return id;
    }

    public (string RawToken, string Email)? Consume(Guid id)
    {
        if (!_pending.TryGetValue(id, out var entry))
            return null;

        _pending.Remove(id);
        return entry.Expiry > DateTime.UtcNow ? (entry.RawToken, entry.Email) : null;
    }
}
