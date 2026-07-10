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

    // US-035 — partita che fa salire team1: entrambi i giocatori vincitori devono comparire nella lista
    // team1 parte a 999, team2 a 1001 → differenza minima → delta ≈ 10 pts basta a sorpassare
    [Fact]
    public async Task ConfirmedMatch_WinnersSalirono_ReturnedInImprovedList()
    {
        using var db = CreateDb();
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();

        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[0], Rating = 999 });
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[1], Rating = 999 });
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[2], Rating = 1001 });
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[3], Rating = 1001 });

        var match = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = 1,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
        };
        db.Matches.Add(match);
        db.MatchSets.AddRange(
            new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 6, Team2Score = 0 },
            new MatchSet { MatchId = match.Id, SetNumber = 2, Team1Score = 6, Team2Score = 0 });
        await db.SaveChangesAsync();

        var improved = await new RatingService().CalculateAndApplyAsync(match.Id, db);
        await db.SaveChangesAsync();

        // team1 (999) wins over team2 (1001) → team1 surpasses team2 in ranking
        var improvedIds = improved.Select(x => x.UserId).ToHashSet();
        Assert.Contains(playerIds[0], improvedIds);
        Assert.Contains(playerIds[1], improvedIds);
    }

    // US-035 — giocatori che scendono (team2) non compaiono nella lista
    [Fact]
    public async Task ConfirmedMatch_LosersNotInImprovedList()
    {
        using var db = CreateDb();
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[0], Rating = 999 });
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[1], Rating = 999 });
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[2], Rating = 1001 });
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[3], Rating = 1001 });

        var match = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = 1,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
        };
        db.Matches.Add(match);
        db.MatchSets.AddRange(
            new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 6, Team2Score = 0 },
            new MatchSet { MatchId = match.Id, SetNumber = 2, Team1Score = 6, Team2Score = 0 });
        await db.SaveChangesAsync();

        var improved = await new RatingService().CalculateAndApplyAsync(match.Id, db);
        await db.SaveChangesAsync();

        var improvedIds = improved.Select(x => x.UserId).ToHashSet();
        Assert.DoesNotContain(playerIds[2], improvedIds);
        Assert.DoesNotContain(playerIds[3], improvedIds);
    }

    // US-035 — più giocatori della stessa partita salgono: ciascuno compare con la propria posizione (AC6)
    [Fact]
    public async Task ConfirmedMatch_MultiplePlayersImproved_EachHasOwnPosition()
    {
        using var db = CreateDb();
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        // team1=999, team2=1001: piccolo gap → team1 vince → entrambi i team1 players salgono
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[0], Rating = 999 });
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[1], Rating = 999 });
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[2], Rating = 1001 });
        db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = playerIds[3], Rating = 1001 });

        var match = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = 1,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
        };
        db.Matches.Add(match);
        db.MatchSets.AddRange(
            new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 6, Team2Score = 0 },
            new MatchSet { MatchId = match.Id, SetNumber = 2, Team1Score = 6, Team2Score = 0 });
        await db.SaveChangesAsync();

        var improved = await new RatingService().CalculateAndApplyAsync(match.Id, db);
        await db.SaveChangesAsync();

        var improvedList = improved.Where(x => new[] { playerIds[0], playerIds[1] }.Contains(x.UserId)).ToList();
        Assert.Equal(2, improvedList.Count);
        Assert.All(improvedList, item => Assert.True(item.NewPosition >= 1));
    }

    // US-035 — posizione nella lista riflette il ranking del circolo (AC5)
    [Fact]
    public async Task ConfirmedMatch_ImprovedPosition_ReflectsCircleRanking()
    {
        using var db = CreateDb();
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        // team1=999, team2=1001: piccolo gap garantisce che il delta ≈10 faccia sorpassare team1
        db.CircleMemberships.AddRange(
            new CircleMembership { CircleId = circleId, UserId = playerIds[0], Rating = 999 },
            new CircleMembership { CircleId = circleId, UserId = playerIds[1], Rating = 999 },
            new CircleMembership { CircleId = circleId, UserId = playerIds[2], Rating = 1001 },
            new CircleMembership { CircleId = circleId, UserId = playerIds[3], Rating = 1001 });

        var match = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = 1,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
        };
        db.Matches.Add(match);
        db.MatchSets.AddRange(
            new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 6, Team2Score = 0 },
            new MatchSet { MatchId = match.Id, SetNumber = 2, Team1Score = 6, Team2Score = 0 });
        await db.SaveChangesAsync();

        var improved = await new RatingService().CalculateAndApplyAsync(match.Id, db);
        await db.SaveChangesAsync();

        var improvedIds = improved.Select(x => x.UserId).ToHashSet();
        Assert.Contains(playerIds[0], improvedIds);
        Assert.Contains(playerIds[1], improvedIds);
        Assert.All(improved, item => Assert.InRange(item.NewPosition, 1, 4));
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

    // US-061 — replay del circolo (senza escludere nessuna partita) deve riprodurre esattamente
    // gli stessi rating finali ottenuti applicando le partite una sola volta: verifica che
    // ResetAndReplayCircleAsync non introduca divergenze rispetto alla formula esistente.
    [Fact]
    public async Task ResetAndReplayCircle_NoExclusion_ReproducesSameFinalRatings()
    {
        using var db = CreateDb();
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        db.CircleMemberships.AddRange(playerIds.Select(p =>
            new CircleMembership { CircleId = circleId, UserId = p, Rating = 1000 }));

        var service = new RatingService();
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

        var originalRatings = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .ToDictionaryAsync(m => m.UserId, m => m.Rating);

        // Replay escludendo un id inesistente: equivale a rigiocare tutta la storia identica
        await service.ResetAndReplayCircleAsync(circleId, Guid.NewGuid(), db);
        await db.SaveChangesAsync();

        var replayedRatings = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .ToDictionaryAsync(m => m.UserId, m => m.Rating);

        Assert.Equal(originalRatings, replayedRatings);
    }

    // US-061 — regressione: il replay deve vedere la partita cancellata come già rimossa dal DB,
    // non solo "tracciata per la rimozione". Se restasse visibile alle query di CountKAsync durante
    // il replay, il K-value (cold-start sotto le 15 partite) delle partite successive risulterebbe
    // gonfiato di 1 partita fantasma. Qui si crea la 16ª partita del circolo (K=32, count=15 prima
    // della cancellazione) e si verifica che, dopo aver fisicamente rimosso una partita precedente
    // e rieseguito il replay, il delta della 16ª partita rifletta K=48 (count=14, sotto cold-start).
    [Fact]
    public async Task ResetAndReplayCircle_PhysicallyRemovedMatch_DoesNotInflateColdStartCount()
    {
        using var db = CreateDb();
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        db.CircleMemberships.AddRange(playerIds.Select(p =>
            new CircleMembership { CircleId = circleId, UserId = p, Rating = 1000 }));
        await db.SaveChangesAsync();

        // Vincitore alternato per tenere i rating vicini a 1000: altrimenti dopo molte partite
        // vinte sempre dalla stessa squadra il margine collassa verso 0 e il delta diventa ±1
        // indipendentemente da K, mascherando la regressione che questo test vuole verificare.
        var service = new RatingService();
        var matchIds = new List<Guid>();
        for (int i = 0; i < 16; i++)
        {
            bool team1Wins = i % 2 == 0;
            var match = new Match
            {
                CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = team1Wins ? 1 : 2,
                Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
                Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i),
            };
            db.Matches.Add(match);
            db.MatchSets.Add(team1Wins
                ? new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 6, Team2Score = 4 }
                : new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 4, Team2Score = 6 });
            await db.SaveChangesAsync();
            await service.CalculateAndApplyAsync(match.Id, db);
            await db.SaveChangesAsync();
            matchIds.Add(match.Id);
        }

        // 16ª partita (indice 15, team2 vince): count prima = 15 confirmed → K=32 (fuori cold-start)
        var match16Before = await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchIds[15]);
        var deltaBefore = Math.Abs(match16Before.DeltaTeam2Player1!.Value);

        // Simula l'endpoint: rimuove fisicamente una partita precedente (indice 0) PRIMA del replay
        var toRemove = await db.Matches.Include(m => m.Sets).SingleAsync(m => m.Id == matchIds[0]);
        db.MatchSets.RemoveRange(toRemove.Sets);
        db.Matches.Remove(toRemove);
        await db.SaveChangesAsync();

        await service.ResetAndReplayCircleAsync(circleId, Guid.NewGuid(), db);
        await db.SaveChangesAsync();

        // Con la partita rimossa, la 16ª ha ora solo 14 confirmed precedenti → K=48 (cold-start)
        var match16After = await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchIds[15]);
        var deltaAfter = Math.Abs(match16After.DeltaTeam2Player1!.Value);

        Assert.True(deltaAfter > deltaBefore,
            $"Atteso delta più alto con K=48 dopo la rimozione fisica (K=32 pre-rimozione): before={deltaBefore}, after={deltaAfter}");
    }

    // US-061 — cancellare (escludere dal replay) una partita intermedia ricostruisce la storia
    // come se quella partita non fosse mai avvenuta: qui una partita "equilibratrice" persa da team1
    // nel mezzo, se esclusa, lascia team1 con rating più alto di quanto risulterebbe includendola.
    [Fact]
    public async Task ResetAndReplayCircle_ExcludingIntermediateMatch_RebuildsHistoryWithoutIt()
    {
        using var db = CreateDb();
        var circleId = Guid.NewGuid();
        var playerIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        db.CircleMemberships.AddRange(playerIds.Select(p =>
            new CircleMembership { CircleId = circleId, UserId = p, Rating = 1000 }));

        var service = new RatingService();

        // Match 1: team1 vince
        var match1 = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = 1,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Matches.Add(match1);
        db.MatchSets.Add(new MatchSet { MatchId = match1.Id, SetNumber = 1, Team1Score = 6, Team2Score = 2 });
        await db.SaveChangesAsync();
        await service.CalculateAndApplyAsync(match1.Id, db);
        await db.SaveChangesAsync();

        // Match 2 (da cancellare): team2 vince, riporta i rating verso 1000
        var match2 = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = 2,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1),
        };
        db.Matches.Add(match2);
        db.MatchSets.Add(new MatchSet { MatchId = match2.Id, SetNumber = 1, Team1Score = 2, Team2Score = 6 });
        await db.SaveChangesAsync();
        await service.CalculateAndApplyAsync(match2.Id, db);
        await db.SaveChangesAsync();

        var ratingsWithBothMatches = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .ToDictionaryAsync(m => m.UserId, m => m.Rating);

        await service.ResetAndReplayCircleAsync(circleId, match2.Id, db);
        await db.SaveChangesAsync();

        var ratingsExcludingMatch2 = await db.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .ToDictionaryAsync(m => m.UserId, m => m.Rating);

        // Senza match2, team1 resta col vantaggio di match1 invece di essere riportato verso 1000
        Assert.True(ratingsExcludingMatch2[playerIds[0]].CompareTo(ratingsWithBothMatches[playerIds[0]]) > 0);
        Assert.True(ratingsExcludingMatch2[playerIds[2]].CompareTo(ratingsWithBothMatches[playerIds[2]]) < 0);

        // Il match escluso non viene toccato dal replay (resta persistito coi suoi delta originali finché
        // non è l'endpoint DELETE a rimuoverlo fisicamente)
        var match2AfterReplay = await db.Matches.AsNoTracking().SingleAsync(m => m.Id == match2.Id);
        Assert.NotNull(match2AfterReplay.DeltaTeam1Player1);
    }
}
