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
        int winnerUnits = team1Won ? totalTeam1 : totalTeam2;
        int loserUnits  = team1Won ? totalTeam2 : totalTeam1;
        int basePoints  = (winnerUnits - loserUnits) + 1;

        var winnerPlayers = PlayersOf(match, team1Won ? 1 : 2);
        var loserPlayers  = PlayersOf(match, team1Won ? 2 : 1);

        var scores = await GetWindowScoresAsync(
            db, match.CircleId, circle.GameBonusWindowMatches, circle.GameBonusWindowWeeks,
            winnerPlayers.Concat(loserPlayers));

        double winnerAvg = winnerPlayers.Average(p => (double)scores.GetValueOrDefault(p, 0));
        double loserAvg  = loserPlayers.Average(p => (double)scores.GetValueOrDefault(p, 0));

        int bonus = winnerAvg < loserAvg
            ? (int)Math.Ceiling(0.10 * (loserAvg - winnerAvg))
            : 0;

        match.GameBonusWinnerPoints = basePoints + bonus;
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
