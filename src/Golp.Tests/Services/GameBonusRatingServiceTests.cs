using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Tests.Services;

public class GameBonusRatingServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"GameBonusTestDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private record Fixture(AppDbContext Db, Circle Circle, Match Match, Guid[] PlayerIds);

    private static async Task<Fixture> SetupAsync(
        AppDbContext db,
        (int t1, int t2)[] sets,
        int winnerTeam = 1,
        string status = "confirmed",
        int windowMatches = 30,
        int windowWeeks = 6)
    {
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();

        var circle = new Circle
        {
            Id                     = circleId,
            OwnerId                = playerIds[0],
            Name                   = "Test Circle",
            Sport                  = "padel",
            PointUnit              = "games",
            Sets                   = true,
            TeamSize               = 2,
            RatingMethod           = "GameBonus",
            GameBonusWindowMatches = windowMatches,
            GameBonusWindowWeeks   = windowWeeks,
        };
        db.Circles.Add(circle);

        foreach (var pid in playerIds)
            db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = pid, Rating = 1000 });

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
        return new Fixture(db, circle, match, playerIds);
    }

    /// <summary>Aggiunge un match confermato con punti Game+Bonus già assegnati, per popolare la finestra.</summary>
    private static async Task<Match> AddHistoricalMatchAsync(
        AppDbContext db, Guid circleId, Guid winner1, Guid winner2, Guid loser1, Guid loser2,
        int points, DateTimeOffset createdAt)
    {
        var m = new Match
        {
            CircleId               = circleId,
            CreatedById            = winner1,
            Status                 = "confirmed",
            WinnerTeam             = 1,
            Team1Player1Id         = winner1,
            Team1Player2Id         = winner2,
            Team2Player1Id         = loser1,
            Team2Player2Id         = loser2,
            GameBonusWinnerPoints  = points,
            CreatedAt              = createdAt,
        };
        db.Matches.Add(m);
        await db.SaveChangesAsync();
        return m;
    }

    // ── Guardie / idempotenza ──────────────────────────────────────────────

    [Fact]
    public async Task PendingMatch_NoPoints()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 4)], status: "pending");

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Null((await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    [Fact]
    public async Task DisputedMatch_NoPoints()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 4)], status: "disputed");

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Null((await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    [Fact]
    public async Task AlreadyProcessed_NotRecalculated()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 4)]);

        var service = new GameBonusRatingService();
        await service.CalculateAndApplyAsync(f.Match.Id, db);
        var firstPoints = (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints;

        // Forza manualmente un altro valore per verificare che una seconda chiamata non lo tocchi
        var match = (await db.Matches.FindAsync(f.Match.Id))!;
        match.GameBonusWinnerPoints = 999;
        await db.SaveChangesAsync();

        await service.CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(999, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
        Assert.NotEqual(999, firstPoints);
    }

    // ── Punteggio base ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(6, 4, 3)]  // differenza 2 + 1 vittoria = 3
    [InlineData(6, 0, 7)]  // differenza 6 + 1 = 7
    public async Task BasePoints_SingleSet(int t1, int t2, int expectedPoints)
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(t1, t2)], winnerTeam: 1);

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(expectedPoints, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    [Fact]
    public async Task BasePoints_MultiSet_SumsGamesAcrossAllSets()
    {
        using var db = CreateDb();
        // 6-2 6-2 → team1: 12 game, team2: 4 game → diff 8 + 1 = 9
        var f = await SetupAsync(db, [(6, 2), (6, 2)], winnerTeam: 1);

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(9, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    [Fact]
    public async Task BasePoints_SuperTiebreak_CountsAsOrdinarySet()
    {
        using var db = CreateDb();
        // 4-6 6-4 7-6 (winner team2 tramite super tiebreak) → team1: 4+6+6=16, team2: 6+4+7=17 → diff 1 + 1 = 2
        var f = await SetupAsync(db, [(4, 6), (6, 4), (6, 7)], winnerTeam: 2);

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(2, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    // ── Bonus upset ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Upset_UnderdogWins_BonusApplied()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 4)], winnerTeam: 1);

        // Storia pregressa: team1 (vincitrice qui) ha punteggio basso, team2 (perdente qui) alto.
        // team1 players: f.PlayerIds[0], f.PlayerIds[1] — media 0 pt
        // team2 players: f.PlayerIds[2], f.PlayerIds[3] — media 100 pt (li facciamo vincere in una storica)
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        await AddHistoricalMatchAsync(db, f.Circle.Id, f.PlayerIds[2], f.PlayerIds[3], Guid.NewGuid(), Guid.NewGuid(), 100, cutoff);

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        // base = (6-4)+1 = 3; differenza = 100-0 = 100; bonus = ceil(0.10*100) = 10; totale = 13
        Assert.Equal(13, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    [Fact]
    public async Task Upset_FavoriteWins_NoBonus()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 4)], winnerTeam: 1);

        // team1 (vincitrice) è già più forte: le diamo storia, team2 (perdente) a 0
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        await AddHistoricalMatchAsync(db, f.Circle.Id, f.PlayerIds[0], f.PlayerIds[1], Guid.NewGuid(), Guid.NewGuid(), 100, cutoff);

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        Assert.Equal(3, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    [Fact]
    public async Task Upset_EqualScores_NoBonus()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 4)], winnerTeam: 1);

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        // Nessuna storia pregressa per nessuno dei due team → entrambi a 0, differenza 0, nessun bonus
        Assert.Equal(3, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    [Fact]
    public async Task Upset_NoPriorHistory_DifferenceIsZero_NoBonus()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 0)], winnerTeam: 1);

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        // base = 6+1=7, nessuna storia → nessun bonus
        Assert.Equal(7, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    // ── Finestra rolling ──────────────────────────────────────────────────

    [Fact]
    public async Task WindowScores_MatchOutsideWeeksWindow_Excluded()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 4)], windowWeeks: 6);

        var tooOld = DateTimeOffset.UtcNow.AddDays(-50); // > 6 settimane
        await AddHistoricalMatchAsync(db, f.Circle.Id, f.PlayerIds[2], f.PlayerIds[3], Guid.NewGuid(), Guid.NewGuid(), 100, tooOld);

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        // La storica è troppo vecchia: non entra nella finestra, nessun bonus (differenza 0)
        Assert.Equal(3, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }

    [Fact]
    public async Task WindowScores_MatchOutsideMatchesWindow_Excluded()
    {
        using var db = CreateDb();
        var f = await SetupAsync(db, [(6, 4)], windowMatches: 1);

        // Due storiche: la più vecchia dà bonus a team2, la più recente (che riempie la finestra di 1) no.
        var older = DateTimeOffset.UtcNow.AddDays(-3);
        var newer = DateTimeOffset.UtcNow.AddDays(-1);
        await AddHistoricalMatchAsync(db, f.Circle.Id, f.PlayerIds[2], f.PlayerIds[3], Guid.NewGuid(), Guid.NewGuid(), 100, older);
        await AddHistoricalMatchAsync(db, f.Circle.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 5, newer);

        await new GameBonusRatingService().CalculateAndApplyAsync(f.Match.Id, db);

        // windowMatches=1 → prende solo la più recente (che non coinvolge i player2/3): nessun bonus
        Assert.Equal(3, (await db.Matches.FindAsync(f.Match.Id))!.GameBonusWinnerPoints);
    }
}
