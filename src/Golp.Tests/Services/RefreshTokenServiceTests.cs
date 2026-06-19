using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Golp.Tests.Services;

public class RefreshTokenServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static RefreshTokenService CreateService(AppDbContext db, int expiryDays = 90)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenExpiryDays"] = expiryDays.ToString(),
            })
            .Build();
        return new RefreshTokenService(db, config);
    }

    [Fact]
    public async Task IssueAsync_PersistsHashNotPlainToken()
    {
        using var db = CreateDb();
        var service = CreateService(db);
        var userId = Guid.NewGuid();

        var plainToken = await service.IssueAsync(userId, "TestAgent/1.0");

        var stored = await db.RefreshTokens.SingleAsync();
        Assert.NotEqual(plainToken, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length); // SHA256 hex
        Assert.Equal("TestAgent/1.0", stored.UserAgent);
    }

    [Fact]
    public async Task IssueAsync_ExpiryReadFromConfig()
    {
        using var db = CreateDb();
        var service = CreateService(db, expiryDays: 7);

        await service.IssueAsync(Guid.NewGuid(), null);

        var stored = await db.RefreshTokens.SingleAsync();
        var expectedExpiry = DateTime.UtcNow.AddDays(7);
        Assert.True(stored.ExpiresAt <= expectedExpiry.AddMinutes(1));
        Assert.True(stored.ExpiresAt >= expectedExpiry.AddMinutes(-1));
    }
}
