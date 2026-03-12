namespace EstimatorMcp.Web.Services.Auth;

public record ApiTokenInfo(int Id, string? Label, DateTime CreatedAt, DateTime? LastUsedAt);

public interface ITokenService
{
    /// <summary>Finds or creates the user with the given email, generates a token, returns raw token.</summary>
    Task<string> CreateTokenAsync(string email, string? label);

    /// <summary>Validates a raw Bearer token, updates LastUsedAt, returns user email or null.</summary>
    Task<string?> ValidateTokenAsync(string rawToken);

    Task<List<ApiTokenInfo>> GetTokensForEmailAsync(string email);

    Task RevokeTokenAsync(int tokenId);
}
