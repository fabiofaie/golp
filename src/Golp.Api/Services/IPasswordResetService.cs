namespace Golp.Api.Services;

public interface IPasswordResetService
{
    Task<string> GenerateTokenAsync(Guid userId);
    Task InvalidatePreviousTokensAsync(Guid userId);
}
