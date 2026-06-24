using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Tests.Services;

public class SportsServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SportsTestDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedAsync(AppDbContext db)
    {
        db.Sports.AddRange(
            new Sport { Key = "padel", DisplayName = "Padel", PointUnit = "games", Sets = true, TeamSize = 2, IsActive = true, SetWeight = 0.4 },
            new Sport { Key = "legacy", DisplayName = "Legacy", PointUnit = "points", Sets = false, TeamSize = 2, IsActive = false, SetWeight = 0.0 }
        );
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAllAsync_OnlyReturnsActiveSports()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var service = new SportsService(db);

        var result = await service.GetAllAsync();

        Assert.Single(result);
        Assert.Equal("padel", result[0].Sport);
    }

    [Fact]
    public async Task GetBySportAsync_ActiveSport_ReturnsDto()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var service = new SportsService(db);

        var result = await service.GetBySportAsync("padel");

        Assert.NotNull(result);
        Assert.Equal("games", result!.PointUnit);
        Assert.True(result.Sets);
        Assert.Equal(2, result.TeamSize);
        Assert.Equal(0.4, result.SetWeight);
    }

    [Fact]
    public async Task GetBySportAsync_InactiveSport_ReturnsNull()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var service = new SportsService(db);

        var result = await service.GetBySportAsync("legacy");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetBySportAsync_UnknownSport_ReturnsNull()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var service = new SportsService(db);

        var result = await service.GetBySportAsync("does-not-exist");

        Assert.Null(result);
    }
}
