using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Golp.Api.Services;

public class JwtService(IConfiguration configuration) : IJwtService
{
    private readonly string _secret = configuration["Jwt:Secret"]!;
    private readonly string _issuer = configuration["Jwt:Issuer"]!;
    private readonly string _audience = configuration["Jwt:Audience"]!;
    private readonly int _expiryMinutes = int.Parse(configuration["Jwt:ExpiryMinutes"] ?? "60");

    public string GenerateToken(Guid userId, string? email, Guid securityStamp, bool isSuperAdmin = false, Guid? impersonatorId = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("security_stamp", securityStamp.ToString())
        };

        if (isSuperAdmin)
        {
            claims.Add(new Claim("super_admin", "true"));
        }

        if (impersonatorId.HasValue)
        {
            claims.Add(new Claim("impersonator_id", impersonatorId.Value.ToString()));
        }

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public bool ValidateToken(string token, out Guid userId, out string email)
    {
        userId = Guid.Empty;
        email = string.Empty;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var handler = new JwtSecurityTokenHandler();

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _issuer,
                ValidAudience = _audience,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.Zero
            }, out _);

            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                   ?? principal.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            var emailClaim = principal.FindFirstValue(JwtRegisteredClaimNames.Email)
                          ?? principal.FindFirstValue(System.Security.Claims.ClaimTypes.Email);

            if (sub == null || emailClaim == null || !Guid.TryParse(sub, out userId))
                return false;

            email = emailClaim;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
