using System.Security.Cryptography;
using System.Text;
using EstimatorMcp.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace EstimatorMcp.Web.Services.Auth;

public class TokenService(AppDbContext db) : ITokenService
{
    public async Task<string> CreateTokenAsync(string email, string? label)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            user = new UserEntity { Email = email, CreatedAt = DateTime.UtcNow };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        var rawToken = GenerateRawToken();
        var tokenHash = HashToken(rawToken);

        db.ApiTokens.Add(new ApiTokenEntity
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            Label = label,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return rawToken;
    }

    public async Task<string?> ValidateTokenAsync(string rawToken)
    {
        var tokenHash = HashToken(rawToken);
        var token = await db.ApiTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (token is null)
            return null;

        token.LastUsedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return token.User.Email;
    }

    public async Task<List<ApiTokenInfo>> GetTokensForEmailAsync(string email)
    {
        return await db.ApiTokens
            .Where(t => t.User.Email == email)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new ApiTokenInfo(t.Id, t.Label, t.CreatedAt, t.LastUsedAt))
            .ToListAsync();
    }

    public async Task RevokeTokenAsync(int tokenId)
    {
        var token = await db.ApiTokens.FindAsync(tokenId);
        if (token is not null)
        {
            db.ApiTokens.Remove(token);
            await db.SaveChangesAsync();
        }
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32); // 256-bit
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(bytes);
    }
}
