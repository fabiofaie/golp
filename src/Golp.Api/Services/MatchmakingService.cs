using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

/// <summary>
/// US-049: motore di suggerimento accoppiamenti per il raduno. Nessuna nuova entità partita,
/// nessun nuovo calcolo punteggio: il tie-break riusa il `RatingMethod` già configurato sul circolo
/// (rating ELO da `CircleMembership.Rating`, oppure punteggio finestra Game+Bonus).
/// Euristica greedy, non ottimo globale — vedi rischi aperti in docs/planning/US-049.md.
/// </summary>
public class MatchmakingService : IMatchmakingService
{
    private const int MaxRounds = 20;

    public async Task<MatchmakingPlan> BuildPlanAsync(
        Guid circleId, IReadOnlyList<Guid> presentUserIds, int courts, string targetMode, int targetValue, AppDbContext db)
    {
        var circle = await db.Circles.FirstAsync(c => c.Id == circleId);

        var scores = circle.RatingMethod == "GameBonus"
            ? await GameBonusRatingService.GetWindowScoresAsync(
                db, circleId, circle.GameBonusWindowMatches, circle.GameBonusWindowWeeks, presentUserIds)
            : await db.CircleMemberships
                .Where(m => m.CircleId == circleId && presentUserIds.Contains(m.UserId))
                .ToDictionaryAsync(m => m.UserId, m => m.Rating);

        // ospiti (non hanno CircleMembership): punteggio iniziale standard, coerente col flusso
        // manuale odierno di registrazione partita con ospite.
        foreach (var id in presentUserIds)
            scores.TryAdd(id, 1000);

        var seasonStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var seasonMatches = await db.Matches
            .Where(m => m.CircleId == circleId && m.Status == "confirmed" && m.CreatedAt >= seasonStart)
            .Select(m => new { m.Team1Player1Id, m.Team1Player2Id, m.Team2Player1Id, m.Team2Player2Id })
            .ToListAsync();

        var pairCounts = new Dictionary<(Guid, Guid), int>();
        foreach (var m in seasonMatches)
        {
            AddPair(pairCounts, m.Team1Player1Id, m.Team1Player2Id);
            AddPair(pairCounts, m.Team2Player1Id, m.Team2Player2Id);
        }

        return BuildPlan(presentUserIds, scores, pairCounts, courts, targetMode, targetValue);
    }

    private static void AddPair(Dictionary<(Guid, Guid), int> counts, Guid a, Guid? b)
    {
        if (b is null) return;
        var key = PairKey(a, b.Value);
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }

    private static (Guid, Guid) PairKey(Guid a, Guid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);

    /// <summary>
    /// Nucleo puro (nessun accesso DB): dati i presenti, i loro punteggi correnti e quante volte
    /// ciascuna coppia ha già giocato insieme in stagione, produce il piano multi-turno.
    /// Riusato sia da <see cref="BuildPlanAsync"/> sia direttamente dai test unitari.
    /// </summary>
    public static MatchmakingPlan BuildPlan(
        IReadOnlyList<Guid> presentUserIds,
        IReadOnlyDictionary<Guid, int> scores,
        IReadOnlyDictionary<(Guid, Guid), int> seasonPairCounts,
        int courts, string targetMode, int targetValue)
    {
        var n = presentUserIds.Count;
        var roundsNeeded = targetMode == "PerPlayer"
            ? (int)Math.Ceiling((double)(targetValue * n) / (courts * 4))
            : (int)Math.Ceiling((double)targetValue / courts);
        roundsNeeded = Math.Clamp(roundsNeeded, 1, MaxRounds);

        var restCount = presentUserIds.ToDictionary(id => id, _ => 0);
        // combina lo storico stagionale con le coppie già formate nel piano corrente, così i turni
        // successivi non ripropongono le stesse combinazioni viste nei turni precedenti dello stesso piano.
        var pairCounts = new Dictionary<(Guid, Guid), int>(seasonPairCounts);

        var rounds = new List<PlannedRound>();

        for (var r = 0; r < roundsNeeded; r++)
        {
            var ordered = presentUserIds
                .OrderByDescending(id => restCount[id])
                .ThenBy(id => Guid.NewGuid())
                .ToList();

            var capacity = courts * 4;
            var playing = ordered.Take(capacity).ToList();
            var resting = ordered.Skip(capacity).ToList();
            foreach (var id in resting)
                restCount[id]++;

            var byScore = playing.OrderByDescending(id => scores.GetValueOrDefault(id, 1000)).ToList();
            var matches = new List<PlannedMatch>();

            for (var i = 0; i + 4 <= byScore.Count; i += 4)
            {
                var group = byScore.Skip(i).Take(4).ToArray();
                var match = BestSplit(group, scores, pairCounts);
                matches.Add(match);

                IncrementPair(pairCounts, match.Team1[0], match.Team1[1]);
                IncrementPair(pairCounts, match.Team2[0], match.Team2[1]);
            }

            rounds.Add(new PlannedRound(r, matches, resting));
        }

        return new MatchmakingPlan(rounds);
    }

    /// <summary>
    /// Tra i 3 possibili modi di dividere 4 giocatori in due coppie, sceglie quello che ripropone
    /// meno volte una coppia già vista (criterio primario), a parità il più bilanciato per punteggio
    /// (criterio secondario, tie-break del `RatingMethod` del circolo).
    /// </summary>
    private static PlannedMatch BestSplit(Guid[] group, IReadOnlyDictionary<Guid, int> scores, IReadOnlyDictionary<(Guid, Guid), int> pairCounts)
    {
        var (a, b, c, d) = (group[0], group[1], group[2], group[3]);
        var splits = new[]
        {
            (Team1: new[] { a, d }, Team2: new[] { b, c }),
            (Team1: new[] { a, b }, Team2: new[] { c, d }),
            (Team1: new[] { a, c }, Team2: new[] { b, d }),
        };

        return splits
            .Select(s => new
            {
                s.Team1,
                s.Team2,
                Repeats = PairCount(pairCounts, s.Team1) + PairCount(pairCounts, s.Team2),
                Imbalance = Math.Abs(
                    (scores.GetValueOrDefault(s.Team1[0], 1000) + scores.GetValueOrDefault(s.Team1[1], 1000)) -
                    (scores.GetValueOrDefault(s.Team2[0], 1000) + scores.GetValueOrDefault(s.Team2[1], 1000))),
            })
            .OrderBy(x => x.Repeats)
            .ThenBy(x => x.Imbalance)
            .Select(x => new PlannedMatch(x.Team1, x.Team2))
            .First();
    }

    private static int PairCount(IReadOnlyDictionary<(Guid, Guid), int> counts, Guid[] pair) =>
        counts.GetValueOrDefault(PairKey(pair[0], pair[1]));

    private static void IncrementPair(Dictionary<(Guid, Guid), int> counts, Guid a, Guid b)
    {
        var key = PairKey(a, b);
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }
}
