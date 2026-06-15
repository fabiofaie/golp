using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

/// <summary>
/// ELO adattato per squadre (PRD §Algoritmo di ranking):
///   team_rating      = media dei rating dei membri
///   E_win            = 1 / (1 + 10^((R_perdenti - R_vincitori) / 400))
///   score_ratio      = punti_vincitori / (punti_vincitori + punti_perdenti)  // [0.5, 1.0]
///   effective_result = 0.5 + (score_ratio - 0.5) × amplifier
///   ΔR               = K × (effective_result - E_win)
/// K è per giocatore: 48 sotto le 15 partite confermate nel circolo, 32 dopo.
/// </summary>
public class RatingService : IRatingService
{
    public const double Amplifier = 0.7;
    public const int KDefault = 32;
    public const int KColdStart = 48;
    public const int ColdStartMatches = 15;

    /// <summary>
    /// Calcola i delta ELO per i 4 giocatori dato il risultato di una partita.
    /// Logica pura, senza accesso al DB — riusabile dal simulatore.
    /// </summary>
    /// <param name="team1Avg">Rating medio squadra 1</param>
    /// <param name="team2Avg">Rating medio squadra 2</param>
    /// <param name="kValues">K per ogni giocatore: [t1p1, t1p2, t2p1, t2p2]</param>
    /// <param name="team1Won">true se ha vinto squadra 1</param>
    /// <param name="scoreRatio">score_ratio già clampato in [0.5, 1.0] per il team vincente</param>
    /// <returns>delta per [t1p1, t1p2, t2p1, t2p2] — positivo se vincitore, negativo se perdente</returns>
    public static int[] ComputeDeltas(
        double team1Avg, double team2Avg,
        int[] kValues,
        bool team1Won,
        double scoreRatio)
    {
        double winnerAvg = team1Won ? team1Avg : team2Avg;
        double loserAvg  = team1Won ? team2Avg : team1Avg;

        double expectedWin    = 1.0 / (1.0 + Math.Pow(10.0, (loserAvg - winnerAvg) / 400.0));
        double effectiveResult = 0.5 + (scoreRatio - 0.5) * Amplifier;
        double margin          = effectiveResult - expectedWin;

        int DeltaFor(int k, bool isWinner)
        {
            var delta = (int)Math.Round(k * margin);
            return isWinner ? delta : -delta;
        }

        return [
            DeltaFor(kValues[0], team1Won),
            DeltaFor(kValues[1], team1Won),
            DeltaFor(kValues[2], !team1Won),
            DeltaFor(kValues[3], !team1Won),
        ];
    }

    public async Task CalculateAndApplyAsync(Guid matchId, AppDbContext db)
    {
        var match = await db.Matches
            .Include(m => m.Sets)
            .SingleOrDefaultAsync(m => m.Id == matchId);

        if (match == null || match.Status != "confirmed")
            return;

        var circle = await db.Circles.FindAsync(match.CircleId);

        // Idempotenza: delta già presenti = match già processato
        if (match.DeltaTeam1Player1 != null)
            return;

        int totalTeam1 = match.Sets.Sum(s => s.Team1Score);
        int totalTeam2 = match.Sets.Sum(s => s.Team2Score);
        if (totalTeam1 + totalTeam2 == 0)
            return;

        var playerIds = new[]
        {
            match.Team1Player1Id, match.Team1Player2Id,
            match.Team2Player1Id, match.Team2Player2Id,
        };

        var memberships = await db.CircleMemberships
            .Where(m => m.CircleId == match.CircleId && playerIds.Contains(m.UserId))
            .ToDictionaryAsync(m => m.UserId);

        if (memberships.Count != 4)
            return; // dati incoerenti: nessun aggiornamento parziale

        double team1Rating = (memberships[match.Team1Player1Id].Rating
                            + memberships[match.Team1Player2Id].Rating) / 2.0;
        double team2Rating = (memberships[match.Team2Player1Id].Rating
                            + memberships[match.Team2Player2Id].Rating) / 2.0;

        bool team1Won   = match.WinnerTeam == 1;
        int winnerUnits = team1Won ? totalTeam1 : totalTeam2;

        var sport     = SportsConfig.GetBySport(circle?.Sport ?? "");
        bool useBlended = (circle?.Sets == true) && (sport?.SetWeight > 0);

        double scoreRatio;
        if (useBlended)
        {
            int setsWonByWinner = match.Sets.Count(s =>
                team1Won ? s.Team1Score > s.Team2Score : s.Team2Score > s.Team1Score);
            int totalSets = match.Sets.Count(s => s.Team1Score != s.Team2Score);
            double setRatio  = totalSets > 0 ? (double)setsWonByWinner / totalSets : 0.5;
            double gameRatio = (double)winnerUnits / (totalTeam1 + totalTeam2);
            scoreRatio = Math.Clamp(sport!.SetWeight * setRatio + (1 - sport.SetWeight) * gameRatio, 0.5, 1.0);
        }
        else
        {
            // PRD: score_ratio in [0.5, 1.0]. Clamp difensivo per sport senza set o quando
            // il vincitore ha meno game totali dei perdenti (es. 6-4, 0-6, 7-6).
            scoreRatio = Math.Clamp((double)winnerUnits / (totalTeam1 + totalTeam2), 0.5, 1.0);
        }

        var kByPlayer = new Dictionary<Guid, int>();
        foreach (var playerId in playerIds)
        {
            var confirmedCount = await db.Matches.CountAsync(m =>
                m.CircleId == match.CircleId
                && m.Id != match.Id
                && m.Status == "confirmed"
                && (m.Team1Player1Id == playerId || m.Team1Player2Id == playerId
                 || m.Team2Player1Id == playerId || m.Team2Player2Id == playerId));
            kByPlayer[playerId] = confirmedCount < ColdStartMatches ? KColdStart : KDefault;
        }

        var kValues = new[]
        {
            kByPlayer[match.Team1Player1Id],
            kByPlayer[match.Team1Player2Id],
            kByPlayer[match.Team2Player1Id],
            kByPlayer[match.Team2Player2Id],
        };

        var deltas = ComputeDeltas(team1Rating, team2Rating, kValues, team1Won, scoreRatio);

        match.DeltaTeam1Player1 = deltas[0];
        match.DeltaTeam1Player2 = deltas[1];
        match.DeltaTeam2Player1 = deltas[2];
        match.DeltaTeam2Player2 = deltas[3];

        memberships[match.Team1Player1Id].Rating += match.DeltaTeam1Player1.Value;
        memberships[match.Team1Player2Id].Rating += match.DeltaTeam1Player2.Value;
        memberships[match.Team2Player1Id].Rating += match.DeltaTeam2Player1.Value;
        memberships[match.Team2Player2Id].Rating += match.DeltaTeam2Player2.Value;

        // SaveChangesAsync è responsabilità del caller: stessa transazione della conferma
    }
}
