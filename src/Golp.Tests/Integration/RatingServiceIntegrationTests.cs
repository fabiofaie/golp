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
        AppDbContext db, string status = "confirmed", bool createCircleWithSets = false)
    {
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();

        if (createCircleWithSets)
        {
            db.Circles.Add(new Circle
            {
                Id        = circleId,
                OwnerId   = playerIds[0],
                Name      = "Test Circle",
                Sport     = "padel",
                PointUnit = "games",
                Sets      = true,
                TeamSize  = 2,
            });

            if (!await db.Sports.AnyAsync(s => s.Key == "padel"))
            {
                db.Sports.Add(new Sport
                {
                    Key = "padel", DisplayName = "Padel", PointUnit = "games",
                    Sets = true, TeamSize = 2, IsActive = true, SetWeight = 0.4,
                });
            }
        }

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

    // US-012 — circolo con Sets=true: verifica formula blended end-to-end
    // sets [(6,2),(6,3)]: set_ratio=1.0, game_ratio=12/17≈0.706
    // blended=0.4×1.0+0.6×0.706≈0.824, effective≈0.727, margin≈0.227, K=48 → round(10.87)=11
    [Fact]
    public async Task SetSport_ConfirmedMatch_BlendedDeltaCorrect()
    {
        using var db = CreateDb();
        var (matchId, circleId, playerIds) = await SeedConfirmedMatchAsync(db, createCircleWithSets: true);

        await new RatingService().CalculateAndApplyAsync(matchId, db);
        await db.SaveChangesAsync();

        var match = await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId);
        var memberships = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .ToDictionaryAsync(m => m.UserId);

        Assert.Equal(11, match.DeltaTeam1Player1);
        Assert.Equal(11, match.DeltaTeam1Player2);
        Assert.Equal(-11, match.DeltaTeam2Player1);
        Assert.Equal(-11, match.DeltaTeam2Player2);
        Assert.Equal(1011, memberships[playerIds[0]].Rating);
        Assert.Equal(989, memberships[playerIds[2]].Rating);
        Assert.Equal(0, match.DeltaTeam1Player1!.Value + match.DeltaTeam2Player1!.Value);
    }

    // US-034 — sets pari (1-1) ma game decidono (10 vs 8): margine solo da game_ratio, nessun delta a 0
    [Fact]
    public async Task SetSport_ConfirmedMatch_SetsTied_UsesGameRatioOnly_NoZeroDelta()
    {
        using var db = CreateDb();
        var (matchId, circleId, playerIds) = await SeedConfirmedMatchAsync(db, createCircleWithSets: true);

        // override dei set seedati col caso US-034: 6-2, 4-6 (1 set a testa, game 10 vs 8)
        var existingSets = await db.MatchSets.Where(s => s.MatchId == matchId).ToListAsync();
        db.MatchSets.RemoveRange(existingSets);
        db.MatchSets.AddRange(
            new MatchSet { MatchId = matchId, SetNumber = 1, Team1Score = 6, Team2Score = 2 },
            new MatchSet { MatchId = matchId, SetNumber = 2, Team1Score = 4, Team2Score = 6 });
        await db.SaveChangesAsync();

        await new RatingService().CalculateAndApplyAsync(matchId, db);
        await db.SaveChangesAsync();

        var match = await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId);
        var memberships = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .ToDictionaryAsync(m => m.UserId);

        Assert.NotEqual(0, match.DeltaTeam1Player1);
        Assert.True(memberships[playerIds[0]].Rating > 1000);
        Assert.True(memberships[playerIds[2]].Rating < 1000);
        Assert.Equal(0, match.DeltaTeam1Player1!.Value + match.DeltaTeam2Player1!.Value);
    }
}
