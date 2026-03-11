using System.Security.Cryptography;
using System.Text;
using EstimatorMcp.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace EstimatorMcp.Web.Services.Auth;

public class VerificationService(AppDbContext db) : IVerificationService
{
    private static readonly TimeSpan CodeTtl = TimeSpan.FromMinutes(15);

    public async Task<string> CreateVerificationAsync(string email)
    {
        // Remove any existing pending verifications for this email
        var existing = await db.PendingVerifications
            .Where(v => v.Email == email)
            .ToListAsync();
        db.PendingVerifications.RemoveRange(existing);

        var code = Random.Shared.Next(100_000, 999_999).ToString();
        var codeHash = HashString(code);

        db.PendingVerifications.Add(new PendingVerificationEntity
        {
            Email = email,
            CodeHash = codeHash,
            ExpiresAt = DateTime.UtcNow.Add(CodeTtl)
        });

        await db.SaveChangesAsync();
        return code;
    }

    public async Task<bool> ValidateCodeAsync(string email, string code)
    {
        var codeHash = HashString(code);
        var verification = await db.PendingVerifications
            .FirstOrDefaultAsync(v => v.Email == email && v.CodeHash == codeHash);

        if (verification is null || verification.ExpiresAt <= DateTime.UtcNow)
            return false;

        db.PendingVerifications.Remove(verification);
        await db.SaveChangesAsync();
        return true;
    }

    private static string HashString(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
