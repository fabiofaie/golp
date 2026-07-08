using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

/// <summary>
/// Metodo di calcolo punteggio alternativo all'ELO (US-052, PRD non ancora aggiornato):
///   punti_vincitore = (game_vincitore - game_perdente) + 1
///   punti_perdente  = 0
///   bonus upset     = ceil(0.10 * (punteggio_perdente - punteggio_vincitore)) se il vincitore
///                     aveva, nella finestra corrente, punteggio medio squadra inferiore al perdente
/// La classifica non è un contatore cumulativo: è la somma dei punti delle sole partite confermate
/// che rientrano sia nelle ultime N partite del circolo (GameBonusWindowMatches) sia nelle ultime
/// M settimane (GameBonusWindowWeeks) — vedi <see cref="GetWindowScoresAsync"/>.
/// </summary>
public class GameBonusRatingService : IGameBonusRatingService
{
    public async Task CalculateAndApplyAsync(Guid matchId, AppDbContext db)
    {
        var match = await db.Matches
            .Include(m => m.Sets)
            .SingleOrDefaultAsync(m => m.Id == matchId);

        if (match == null || match.Status != "confirmed")
            return;

        // Idempotenza: punti già presenti = match già processato con questo metodo
        if (match.GameBonusWinnerPoints != null)
            return;

        int totalTeam1 = match.Sets.Sum(s => s.Team1Score);
        int totalTeam2 = match.Sets.Sum(s => s.Team2Score);
        if (totalTeam1 + totalTeam2 == 0)
            return;

        var circle = await db.Circles.FindAsync(match.CircleId);
        if (circle == null)
            return;

        bool team1Won = match.WinnerTeam == 1;

        // Sport senza set (basket2v2, burraco): nessuna riga in match.Sets, ma i totali già
        // calcolati fungono da singolo "set virtuale" — preserva il comportamento pre-US-056.
        List<(int Team1, int Team2)> sets = match.Sets.Count > 0
            ? match.Sets.Select(s => (s.Team1Score, s.Team2Score)).ToList()
            : [(totalTeam1, totalTeam2)];

        var winnerPlayers = PlayersOf(match, team1Won ? 1 : 2);
        var loserPlayers  = PlayersOf(match, team1Won ? 2 : 1);

        var scores = await GetWindowScoresAsync(
            db, match.CircleId, circle.GameBonusWindowMatches, circle.GameBonusWindowWeeks,
            winnerPlayers.Concat(loserPlayers));

        double winnerAvg = winnerPlayers.Average(p => (double)scores.GetValueOrDefault(p, 0));
        double loserAvg  = loserPlayers.Average(p => (double)scores.GetValueOrDefault(p, 0));

        var points = ComputeMatchPoints(sets, team1Won, winnerAvg, loserAvg);
        match.GameBonusWinnerPoints = points;

        // Espone il punteggio Game+Bonus della partita come "delta" per la UI di dettaglio match
        // (vincitori = punti assegnati, perdenti = 0), analogamente ai delta ELO.
        match.DeltaTeam1Player1 = team1Won ? points : 0;
        match.DeltaTeam2Player1 = team1Won ? 0 : points;
        if (match.Team1Player2Id.HasValue)
            match.DeltaTeam1Player2 = team1Won ? points : 0;
        if (match.Team2Player2Id.HasValue)
            match.DeltaTeam2Player2 = team1Won ? 0 : points;
    }

    /// <summary>
    /// Calcolo puro (nessun accesso a DB) dei punti assegnati al vincitore di una partita Game+Bonus:
    ///   base  = (set_vinti - set_persi) + somma, sui SOLI set vinti dalla squadra vincente,
    ///           di (game_vincitore - game_perdente) in quel set (i set persi dal vincitore
    ///           non contribuiscono né penalizzano — US-056, evita che un set perso largo
    ///           ribalti il segno del punteggio di chi ha comunque vinto la partita)
    ///   bonus = ceil(0.10 * (punteggio_perdente - punteggio_vincitore)) se il vincitore aveva,
    ///           prima della partita, punteggio Game+Bonus medio inferiore al perdente; 0 altrimenti.
    /// Per sport senza set / risultato unico, <paramref name="sets"/> ha un solo elemento: la formula
    /// collassa esattamente sul comportamento pre-US-056 (game diff + 1).
    /// Riusato sia dal calcolo reale (<see cref="CalculateAndApplyAsync"/>) sia dal simulatore pubblico.
    /// </summary>
    public static int ComputeMatchPoints(IReadOnlyList<(int Team1, int Team2)> sets, bool team1Won, double winnerCurrentScore, double loserCurrentScore)
    {
        int setsWon = 0, setsLost = 0, gameDiffPerSetVinto = 0;

        foreach (var (t1, t2) in sets)
        {
            int winnerGames = team1Won ? t1 : t2;
            int loserGames  = team1Won ? t2 : t1;

            if (winnerGames > loserGames)
            {
                setsWon++;
                gameDiffPerSetVinto += winnerGames - loserGames;
            }
            else if (winnerGames < loserGames)
            {
                setsLost++;
            }
        }

        int basePoints = (setsWon - setsLost) + gameDiffPerSetVinto;

        int bonus = winnerCurrentScore < loserCurrentScore
            ? (int)Math.Ceiling(0.10 * (loserCurrentScore - winnerCurrentScore))
            : 0;

        return basePoints + bonus;
    }

    private static List<Guid> PlayersOf(Match match, int team) => team == 1
        ? (match.Team1Player2Id.HasValue ? [match.Team1Player1Id, match.Team1Player2Id.Value] : [match.Team1Player1Id])
        : (match.Team2Player2Id.HasValue ? [match.Team2Player1Id, match.Team2Player2Id.Value] : [match.Team2Player1Id]);

    /// <summary>
    /// Punteggio Game+Bonus corrente per ciascuno dei giocatori indicati, calcolato sulla finestra
    /// (ultime <paramref name="windowMatches"/> partite del circolo ∩ ultime <paramref name="windowWeeks"/> settimane).
    /// Riusata sia per il bonus upset (con la lista dei 2/4 giocatori coinvolti) sia dalla classifica del circolo
    /// (con tutti i membri).
    /// </summary>
    public static async Task<Dictionary<Guid, int>> GetWindowScoresAsync(
        AppDbContext db, Guid circleId, int windowMatches, int windowWeeks, IEnumerable<Guid> playerIds)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-7 * windowWeeks);

        var windowedMatches = await db.Matches
            .Where(m => m.CircleId == circleId
                     && m.Status == "confirmed"
                     && m.GameBonusWinnerPoints != null
                     && m.CreatedAt >= cutoff)
            .OrderByDescending(m => m.CreatedAt)
            .Take(windowMatches)
            .Select(m => new
            {
                m.WinnerTeam,
                m.Team1Player1Id, m.Team1Player2Id,
                m.Team2Player1Id, m.Team2Player2Id,
                m.GameBonusWinnerPoints,
            })
            .ToListAsync();

        var scores = playerIds.Distinct().ToDictionary(id => id, _ => 0);

        foreach (var m in windowedMatches)
        {
            var winners = m.WinnerTeam == 1
                ? (m.Team1Player2Id.HasValue ? new[] { m.Team1Player1Id, m.Team1Player2Id.Value } : [m.Team1Player1Id])
                : (m.Team2Player2Id.HasValue ? new[] { m.Team2Player1Id, m.Team2Player2Id.Value } : [m.Team2Player1Id]);

            foreach (var winnerId in winners)
                if (scores.ContainsKey(winnerId))
                    scores[winnerId] += m.GameBonusWinnerPoints!.Value;
        }

        return scores;
    }
}
