using System.IdentityModel.Tokens.Jwt;
using Golp.Api.Services;
using Microsoft.Extensions.Configuration;

namespace Golp.Tests.Services;

public class JwtServiceTests
{
    private static JwtService CreateService(int expiryMinutes = 60)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-secret-key-minimum-32-characters-long",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:ExpiryMinutes"] = expiryMinutes.ToString()
            })
            .Build();

        return new JwtService(config);
    }

    [Fact]
    public void GenerateToken_ReturnsValidJwt_WithCorrectClaims()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();
        var email = "test@example.com";

        var token = service.GenerateToken(userId, email, Guid.NewGuid());

        Assert.NotEmpty(token);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Equal(email, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
    }

    [Fact]
    public void GenerateToken_IncludesSecurityStampClaim()
    {
        var service = CreateService();
        var securityStamp = Guid.NewGuid();

        var token = service.GenerateToken(Guid.NewGuid(), "test@example.com", securityStamp);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal(securityStamp.ToString(), jwt.Claims.First(c => c.Type == "security_stamp").Value);
    }

    [Fact]
    public void GenerateToken_ExpiresAtConfiguredTime()
    {
        var service = CreateService(expiryMinutes: 60);
        var token = service.GenerateToken(Guid.NewGuid(), "test@example.com", Guid.NewGuid());

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var expectedExpiry = DateTime.UtcNow.AddMinutes(60);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        Assert.True(jwt.ValidTo <= expectedExpiry.AddSeconds(5));
    }

    [Fact]
    public void ValidateToken_ValidToken_ReturnsTrue_WithCorrectClaims()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();
        var email = "user@example.com";

        var token = service.GenerateToken(userId, email, Guid.NewGuid());
        var result = service.ValidateToken(token, out var parsedId, out var parsedEmail);

        Assert.True(result);
        Assert.Equal(userId, parsedId);
        Assert.Equal(email, parsedEmail);
    }

    [Fact]
    public void ValidateToken_ExpiredToken_ReturnsFalse()
    {
        var service = CreateService(expiryMinutes: -1);
        var token = service.GenerateToken(Guid.NewGuid(), "test@example.com", Guid.NewGuid());

        var result = service.ValidateToken(token, out _, out _);

        Assert.False(result);
    }

    [Fact]
    public void ValidateToken_TamperedToken_ReturnsFalse()
    {
        var service = CreateService();
        var token = service.GenerateToken(Guid.NewGuid(), "test@example.com", Guid.NewGuid());
        var tampered = token[..^5] + "XXXXX";

        var result = service.ValidateToken(tampered, out _, out _);

        Assert.False(result);
    }
}
