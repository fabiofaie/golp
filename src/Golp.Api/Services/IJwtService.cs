namespace Golp.Api.Services;

public interface IJwtService
{
    string GenerateToken(Guid userId, string email, Guid securityStamp);
    bool ValidateToken(string token, out Guid userId, out string email);
}
