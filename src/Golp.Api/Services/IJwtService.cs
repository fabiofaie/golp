namespace Golp.Api.Services;

public interface IJwtService
{
    string GenerateToken(Guid userId, string email);
    bool ValidateToken(string token, out Guid userId, out string email);
}
