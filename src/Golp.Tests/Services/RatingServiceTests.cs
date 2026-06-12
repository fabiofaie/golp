using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Tests.Services;

public class RatingServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"RatingTestDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private record Fixture(
        AppDbContext Db,
        Match Match,
        Guid[] PlayerIds,
        Dictionary<Guid, CircleMembership> Memberships);

    /// <summary>4 giocatori nello stesso circolo, match team1 vs team2 con i set dati.</summary>
    private static async Task<Fixture> SetupAsync(
        AppDbContext db,
        (int t1, int t2)[] sets,
        int winnerTeam = 1,
        string status = "confirmed",
        int[]? ratings = null)
    {
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        ratings ??= [1000, 1000, 1000, 1000];

        var memberships = new Dictionary<Guid, CircleMembership>();
        for (int i = 0; i < 4; i++)
        {
            var m = new CircleMembership { CircleId = circleId, UserId = playerIds[i], Rating = ratings[i] };
            memberships[playerIds[i]] = m;
            db.CircleMemberships.Add(m);
        }

        var match = new Match
        {
            CircleId       = circleId,
            CreatedById    = playerIds[0],
            Status         = status,
            WinnerTeam     = winnerTeam,
            Team1Player1Id = playerIds[0],
            Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2],
            Team2Player2Id = playerIds[3],
        };
        db.Matches.Add(match);

        for (int i = 0; i < sets.Length; i++)
        {
            db.MatchSets.Add(new MatchSet
            {
                MatchId    = match.Id,
                SetNumber  = i + 1,
                Team1Score = sets[i].t1,
                Team2Score = sets[i].t2,
            });
        }

        await db.SaveChangesAsync();
        return new Fixture(db, match, playerIds, memberships);
    }

    /// <summary>Aggiunge N partite confermate nel circolo che coinvolgono il player.</summary>
    private static async Task AddConfirmedMatchesAsync(AppDbContext db, Guid circleId, Guid playerId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            db.Matches.Add(new Match
            {
                CircleId       = circleId,
                CreatedById    = playerId,
                Status         = "confirmed",
                WinnerTeam     = 1,
                Team1Player1Id = playerId,
                Team1Player2Id = Guid.NewGuid(),
                Team2Player1Id = Guid.NewGuid(),
                Team2Player2Id = Guid.NewGuid(),
            });
        }
        await db.SaveChangesAsync();
    }

    // test1 — tutti a 1000, score_ratio=1.0, cold start (0 confermate) →
    // E_win=0.5, effective=0.5+0.5*0.7=0.85, delta=round(48*0.35)=17
    [Fact]
    public async Task AllEqual_MaxScoreRatio_MaxPositiveDeltaForWinners()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 0), (6, 0)]);

        await new RatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(17, f.Match.DeltaTeam1Player1);
        Assert.Equal(17, f.Match.DeltaTeam1Player2);
        Assert.Equal(-17, f.Match.DeltaTeam2Player1);
        Assert.Equal(-17, f.Match.DeltaTeam2Player2);
        Assert.Equal(1017, f.Memberships[f.PlayerIds[0]].Rating);
        Assert.Equal(1017, f.Memberships[f.PlayerIds[1]].Rating);
        Assert.Equal(983, f.Memberships[f.PlayerIds[2]].Rating);
        Assert.Equal(983, f.Memberships[f.PlayerIds[3]].Rating);
    }

    // test2 — score_ratio=0.5 a rating pari → effective=0.5=E_win → delta 0
    [Fact]
    public async Task EqualRatings_HalfScoreRatio_DeltaZero()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(10, 10)]);

        await new RatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(0, f.Match.DeltaTeam1Player1);
        Assert.Equal(0, f.Match.DeltaTeam2Player1);
        Assert.Equal(1000, f.Memberships[f.PlayerIds[0]].Rating);
        Assert.Equal(1000, f.Memberships[f.PlayerIds[2]].Rating);
    }

    // test3 — K cold start: 14 confermate → K=48 (delta 17), 15 confermate → K=32 (delta 11)
    [Fact]
    public async Task ColdStartBoundary_14ConfirmedK48_15ConfirmedK32()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 0), (6, 0)]);
        await AddConfirmedMatchesAsync(db, f.Match.CircleId, f.PlayerIds[0], 15); // → K=32
        await AddConfirmedMatchesAsync(db, f.Match.CircleId, f.PlayerIds[1], 14); // → K=48

        await new RatingService().CalculateAndApplyAsync(f.Match.Id, db);

        // margin = 0.85 - 0.5 = 0.35 → K32: round(11.2)=11, K48: round(16.8)=17
        Assert.Equal(11, f.Match.DeltaTeam1Player1);
        Assert.Equal(17, f.Match.DeltaTeam1Player2);
    }

    // test4 — idempotenza: delta già presenti → nessuna modifica
    [Fact]
    public async Task AlreadyProcessed_NoChanges()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 0), (6, 0)]);
        var service = new RatingService();

        await service.CalculateAndApplyAsync(f.Match.Id, db);
        await db.SaveChangesAsync();
        var ratingAfterFirst = f.Memberships[f.PlayerIds[0]].Rating;

        await service.CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(ratingAfterFirst, f.Memberships[f.PlayerIds[0]].Rating);
        Assert.Equal(17, f.Match.DeltaTeam1Player1);
    }

    // test5 — match pending → nessuna modifica
    [Fact]
    public async Task PendingMatch_NoChanges()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 0), (6, 0)], status: "pending");

        await new RatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Null(f.Match.DeltaTeam1Player1);
        Assert.Equal(1000, f.Memberships[f.PlayerIds[0]].Rating);
    }

    // test6 — totalUnits=0 → guard divisione per zero, nessuna eccezione né modifica
    [Fact]
    public async Task ZeroTotalUnits_NoExceptionNoChanges()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(0, 0)]);

        await new RatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Null(f.Match.DeltaTeam1Player1);
        Assert.Equal(1000, f.Memberships[f.PlayerIds[0]].Rating);
    }

    // extra — score_ratio da [6-2, 6-3]: totalA=12, totalB=5, ratio=12/17≈0.7059
    // effective=0.5+0.2059*0.7≈0.6441, margin≈0.1441, K=48 → round(6.92)=7
    [Fact]
    public async Task ScoreRatio_FromMultipleSets_MatchesManualCalculation()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 2), (6, 3)]);

        await new RatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(7, f.Match.DeltaTeam1Player1);
        Assert.Equal(-7, f.Match.DeltaTeam2Player1);
    }

    // extra — vincitore con meno unità totali (es. 6-4, 0-6, 7-6): ratio clampato a 0.5
    // → effective=0.5, E_win=0.5 a rating pari → delta 0, mai negativo per chi vince
    [Fact]
    public async Task WinnerWithFewerUnits_RatioClampedAtHalf()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 4), (0, 6), (7, 6)]); // team1: 13 unità, team2: 16

        await new RatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(0, f.Match.DeltaTeam1Player1);
        Assert.Equal(0, f.Match.DeltaTeam2Player1);
    }

    // extra — vince team 2: delta positivi a team2, negativi a team1
    [Fact]
    public async Task Team2Wins_DeltasSignedCorrectly()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(0, 6), (0, 6)], winnerTeam: 2);

        await new RatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(-17, f.Match.DeltaTeam1Player1);
        Assert.Equal(17, f.Match.DeltaTeam2Player1);
        Assert.Equal(983, f.Memberships[f.PlayerIds[0]].Rating);
        Assert.Equal(1017, f.Memberships[f.PlayerIds[2]].Rating);
    }
}
