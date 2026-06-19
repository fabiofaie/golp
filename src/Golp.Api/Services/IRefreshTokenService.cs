namespace Golp.Api.Services;

public record RotateResult(bool Success, string? NewPlainToken, Guid UserId);

public interface IRefreshTokenService
{
    Task<string> IssueAsync(Guid userId, string? userAgent, Guid? familyId = null);
    Task<RotateResult> RotateAsync(string plainToken, string? userAgent);
    Task RevokeAsync(string plainToken);
    Task RevokeAllForUserAsync(Guid userId);
}
