using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Tests.Integration;

/// <summary>
/// Integration a livello di servizio + DbContext (InMemory): verifica il contratto
/// [CalculateAndApplyAsync → SaveChangesAsync] usato da ConfirmMatchAsync (US-005).
/// </summary>
public class RatingServiceIntegrationTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RatingIntegrationDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Guid MatchId, Guid CircleId, Guid[] PlayerIds)> SeedConfirmedMatchAsync(
        AppDbContext db, string status = "confirmed")
    {
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();

        foreach (var pid in playerIds)
            db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = pid, Rating = 1000 });

        var match = new Match
        {
            CircleId       = circleId,
            CreatedById    = playerIds[0],
            Status         = status,
            WinnerTeam     = 1,
            Team1Player1Id = playerIds[0],
            Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2],
            Team2Player2Id = playerIds[3],
        };
        db.Matches.Add(match);
        db.MatchSets.AddRange(
            new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 6, Team2Score = 2 },
            new MatchSet { MatchId = match.Id, SetNumber = 2, Team1Score = 6, Team2Score = 3 });

        await db.SaveChangesAsync();
        return (match.Id, circleId, playerIds);
    }

    [Fact]
    public async Task ConfirmedMatch_RatingsUpdatedAndDeltasPersisted()
    {
        using var db = CreateDb();
        var (matchId, circleId, playerIds) = await SeedConfirmedMatchAsync(db);

        await new RatingService().CalculateAndApplyAsync(matchId, db);
        await db.SaveChangesAsync();

        // AsNoTracking: verifica i valori persistiti, non solo il tracking in memoria
        var match = await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId);
        var memberships = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .ToDictionaryAsync(m => m.UserId);

        // Team 1 vince → rating su, team 2 giù
        Assert.True(memberships[playerIds[0]].Rating > 1000);
        Assert.True(memberships[playerIds[1]].Rating > 1000);
        Assert.True(memberships[playerIds[2]].Rating < 1000);
        Assert.True(memberships[playerIds[3]].Rating < 1000);

        // Delta persistiti sul match
        Assert.NotNull(match.DeltaTeam1Player1);
        Assert.NotNull(match.DeltaTeam1Player2);
        Assert.NotNull(match.DeltaTeam2Player1);
        Assert.NotNull(match.DeltaTeam2Player2);

        // Somma a zero (a meno di arrotondamento; qui K uguale per tutti → esatta)
        var sumTeam1 = match.DeltaTeam1Player1!.Value + match.DeltaTeam1Player2!.Value;
        var sumTeam2 = match.DeltaTeam2Player1!.Value + match.DeltaTeam2Player2!.Value;
        Assert.Equal(sumTeam1, -sumTeam2);
    }

    [Fact]
    public async Task SecondCall_Idempotent_RatingsUnchanged()
    {
        using var db = CreateDb();
        var (matchId, circleId, playerIds) = await SeedConfirmedMatchAsync(db);
        var service = new RatingService();

        await service.CalculateAndApplyAsync(matchId, db);
        await db.SaveChangesAsync();

        var ratingsAfterFirst = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .ToDictionaryAsync(m => m.UserId, m => m.Rating);

        await service.CalculateAndApplyAsync(matchId, db);
        await db.SaveChangesAsync();

        var ratingsAfterSecond = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .ToDictionaryAsync(m => m.UserId, m => m.Rating);

        Assert.Equal(ratingsAfterFirst, ratingsAfterSecond);
    }

    [Fact]
    public async Task PendingMatch_RatingsUnchanged()
    {
        using var db = CreateDb();
        var (matchId, circleId, playerIds) = await SeedConfirmedMatchAsync(db, status: "pending");

        await new RatingService().CalculateAndApplyAsync(matchId, db);
        await db.SaveChangesAsync();

        var ratings = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .Select(m => m.Rating)
            .ToListAsync();

        Assert.All(ratings, r => Assert.Equal(1000, r));
        var match = await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId);
        Assert.Null(match.DeltaTeam1Player1);
    }
}
