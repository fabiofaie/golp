using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Tests.Integration;

/// <summary>
/// US-062 — replay del circolo per il metodo Game+Bonus: verifica che
/// <see cref="GameBonusRatingService.ResetAndReplayCircleAsync"/> non introduca divergenze
/// rispetto al calcolo sequenziale originale, e che modificare (escludere dal calcolo originale,
/// poi rigiocare) una partita intermedia si propaghi correttamente alle successive nella finestra.
/// </summary>
public class GameBonusRatingServiceReplayTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"GameBonusReplayDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static Circle MakeCircle(Guid circleId, Guid ownerId) => new()
    {
        Id = circleId, OwnerId = ownerId, Name = "Test Circle", Sport = "padel", PointUnit = "games",
        Sets = true, TeamSize = 2, RatingMethod = "GameBonus", GameBonusWindowMatches = 30, GameBonusWindowWeeks = 52,
    };

    [Fact]
    public async Task ResetAndReplayCircle_NoChanges_ReproducesSamePoints()
    {
        using var db = CreateDb();
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        db.Circles.Add(MakeCircle(circleId, playerIds[0]));
        db.CircleMemberships.AddRange(playerIds.Select(p =>
            new CircleMembership { CircleId = circleId, UserId = p, Rating = 1000 }));

        var service = new GameBonusRatingService();
        var matchIds = new List<Guid>();
        for (int i = 0; i < 3; i++)
        {
            var match = new Match
            {
                CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = 1,
                Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
                Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i),
            };
            db.Matches.Add(match);
            db.MatchSets.Add(new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 6, Team2Score = 2 });
            await db.SaveChangesAsync();
            await service.CalculateAndApplyAsync(match.Id, db);
            await db.SaveChangesAsync();
            matchIds.Add(match.Id);
        }

        var originalPoints = await db.Matches.AsNoTracking()
            .Where(m => matchIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.GameBonusWinnerPoints);

        await service.ResetAndReplayCircleAsync(circleId, db);
        await db.SaveChangesAsync();

        var replayedPoints = await db.Matches.AsNoTracking()
            .Where(m => matchIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.GameBonusWinnerPoints);

        Assert.Equal(originalPoints, replayedPoints);
    }

    [Fact]
    public async Task EditIntermediateMatch_ReplayPropagatesToSubsequentMatchBonus()
    {
        using var db = CreateDb();
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        db.Circles.Add(MakeCircle(circleId, playerIds[0]));
        db.CircleMemberships.AddRange(playerIds.Select(p =>
            new CircleMembership { CircleId = circleId, UserId = p, Rating = 1000 }));

        var service = new GameBonusRatingService();

        // Match 1: team1 vince largo (6-1) — alza molto lo score di team1, penalizzando l'eventuale
        // bonus upset che team2 potrebbe ricevere in una partita successiva.
        var match1 = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = 1,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Matches.Add(match1);
        db.MatchSets.Add(new MatchSet { MatchId = match1.Id, SetNumber = 1, Team1Score = 20, Team2Score = 1 });
        await db.SaveChangesAsync();
        await service.CalculateAndApplyAsync(match1.Id, db);
        await db.SaveChangesAsync();

        // Match 2: team2 vince di misura — riceve bonus upset perché il suo score medio è più basso.
        var match2 = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = 2,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1),
        };
        db.Matches.Add(match2);
        db.MatchSets.Add(new MatchSet { MatchId = match2.Id, SetNumber = 1, Team1Score = 4, Team2Score = 6 });
        await db.SaveChangesAsync();
        await service.CalculateAndApplyAsync(match2.Id, db);
        await db.SaveChangesAsync();

        var match2PointsBefore = (await db.Matches.AsNoTracking().SingleAsync(m => m.Id == match2.Id)).GameBonusWinnerPoints;

        // "Modifica" match1: 20-1 diventa 20-19 (vittoria molto più risicata) — team1 accumula
        // molti meno punti, riducendo il gap di score che alimenta il bonus upset di match2.
        var match1Sets = await db.MatchSets.Where(s => s.MatchId == match1.Id).ToListAsync();
        db.MatchSets.RemoveRange(match1Sets);
        db.MatchSets.Add(new MatchSet { MatchId = match1.Id, SetNumber = 1, Team1Score = 20, Team2Score = 19 });
        await db.SaveChangesAsync();

        await service.ResetAndReplayCircleAsync(circleId, db);
        await db.SaveChangesAsync();

        var match1PointsAfter = (await db.Matches.AsNoTracking().SingleAsync(m => m.Id == match1.Id)).GameBonusWinnerPoints;
        var match2PointsAfter = (await db.Matches.AsNoTracking().SingleAsync(m => m.Id == match2.Id)).GameBonusWinnerPoints;

        Assert.Equal(2, match1PointsAfter);
        Assert.NotEqual(match2PointsBefore, match2PointsAfter);
    }
}
