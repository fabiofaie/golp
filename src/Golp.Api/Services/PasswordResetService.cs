using System.Security.Cryptography;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

public class PasswordResetService(AppDbContext db) : IPasswordResetService
{
    private const int TokenExpiryHours = 1;

    public async Task<string> GenerateTokenAsync(Guid userId)
    {
        await InvalidatePreviousTokensAsync(userId);

        var plainToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = ComputeSha256(plainToken);

        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddHours(TokenExpiryHours)
        });

        await db.SaveChangesAsync();
        return plainToken;
    }

    public async Task InvalidatePreviousTokensAsync(Guid userId)
    {
        var existing = await db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ToListAsync();

        foreach (var t in existing)
            t.UsedAt = DateTime.UtcNow;

        if (existing.Count > 0)
            await db.SaveChangesAsync();
    }

    public static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
