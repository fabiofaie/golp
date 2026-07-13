using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Tests.Integration;

/// <summary>US-049: round-trip EF dell'entità CircleAttendance.</summary>
public class CircleAttendanceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"CircleAttendanceTestDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task AddAndRetrieve_RoundTrips()
    {
        using var db = CreateDb();
        var owner = new User { Name = "Owner", Email = "owner@test.com", PasswordHash = "x" };
        var circle = new Circle { Name = "C1", Sport = "padel", PointUnit = "games", Owner = owner, OwnerId = owner.Id };
        db.Users.Add(owner);
        db.Circles.Add(circle);
        await db.SaveChangesAsync();

        var attendance = new CircleAttendance { CircleId = circle.Id, UserId = owner.Id };
        db.CircleAttendances.Add(attendance);
        await db.SaveChangesAsync();

        var reloaded = await db.CircleAttendances.FirstOrDefaultAsync(a => a.CircleId == circle.Id && a.UserId == owner.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(attendance.Id, reloaded!.Id);
    }

    [Fact]
    public async Task Remove_DeletesRow()
    {
        using var db = CreateDb();
        var owner = new User { Name = "Owner", Email = "owner2@test.com", PasswordHash = "x" };
        var circle = new Circle { Name = "C2", Sport = "padel", PointUnit = "games", Owner = owner, OwnerId = owner.Id };
        db.Users.Add(owner);
        db.Circles.Add(circle);
        var attendance = new CircleAttendance { CircleId = circle.Id, UserId = owner.Id };
        db.CircleAttendances.Add(attendance);
        await db.SaveChangesAsync();

        db.CircleAttendances.Remove(attendance);
        await db.SaveChangesAsync();

        Assert.False(await db.CircleAttendances.AnyAsync(a => a.CircleId == circle.Id));
    }
}
