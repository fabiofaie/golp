namespace Golp.Api.Services;

public interface IJwtService
{
    string GenerateToken(Guid userId, string? email, Guid securityStamp, bool isSuperAdmin = false, Guid? impersonatorId = null);
    bool ValidateToken(string token, out Guid userId, out string email);
}
