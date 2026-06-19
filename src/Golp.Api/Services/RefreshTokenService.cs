using System.Security.Cryptography;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

public class RefreshTokenService(AppDbContext db, IConfiguration configuration) : IRefreshTokenService
{
    private readonly int _expiryDays = int.Parse(configuration["Jwt:RefreshTokenExpiryDays"] ?? "90");

    public async Task<string> IssueAsync(Guid userId, string? userAgent, Guid? familyId = null)
    {
        var plainToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId    = userId,
            TokenHash = ComputeSha256(plainToken),
            FamilyId  = familyId ?? Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(_expiryDays),
            UserAgent = userAgent,
        });

        await db.SaveChangesAsync();
        return plainToken;
    }

    public async Task<RotateResult> RotateAsync(string plainToken, string? userAgent)
    {
        var tokenHash = ComputeSha256(plainToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (token == null || token.ExpiresAt < DateTime.UtcNow)
            return new RotateResult(false, null, Guid.Empty);

        if (token.RevokedAt != null)
        {
            // Riuso di un token già ruotato/revocato: segnale di furto, revoca tutta la famiglia
            await RevokeFamilyAsync(token.FamilyId);
            return new RotateResult(false, null, Guid.Empty);
        }

        token.RevokedAt = DateTime.UtcNow;
        token.LastUsedAt = DateTime.UtcNow;

        var newPlainToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        token.ReplacedByTokenHash = ComputeSha256(newPlainToken);

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId    = token.UserId,
            TokenHash = token.ReplacedByTokenHash,
            FamilyId  = token.FamilyId,
            ExpiresAt = DateTime.UtcNow.AddDays(_expiryDays),
            UserAgent = userAgent,
        });

        await db.SaveChangesAsync();
        return new RotateResult(true, newPlainToken, token.UserId);
    }

    public async Task RevokeAsync(string plainToken)
    {
        var tokenHash = ComputeSha256(plainToken);
        var token = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash);
        if (token == null || token.RevokedAt != null)
            return;

        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task RevokeAllForUserAsync(Guid userId)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var t in tokens)
            t.RevokedAt = DateTime.UtcNow;

        if (tokens.Count > 0)
            await db.SaveChangesAsync();
    }

    private async Task RevokeFamilyAsync(Guid familyId)
    {
        var tokens = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync();

        foreach (var t in tokens)
            t.RevokedAt = DateTime.UtcNow;

        if (tokens.Count > 0)
            await db.SaveChangesAsync();
    }

    private static string ComputeSha256(string input)
    {
        var bytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(bytes);
    }
}
