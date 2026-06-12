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
    private const double Amplifier = 0.7;
    private const int KDefault = 32;
    private const int KColdStart = 48;
    private const int ColdStartMatches = 15;

    public async Task CalculateAndApplyAsync(Guid matchId, AppDbContext db)
    {
        var match = await db.Matches
            .Include(m => m.Sets)
            .SingleOrDefaultAsync(m => m.Id == matchId);

        if (match == null || match.Status != "confirmed")
            return;

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

        bool team1Won = match.WinnerTeam == 1;
        double winnerRating = team1Won ? team1Rating : team2Rating;
        double loserRating  = team1Won ? team2Rating : team1Rating;
        int winnerUnits     = team1Won ? totalTeam1 : totalTeam2;

        double expectedWin = 1.0 / (1.0 + Math.Pow(10.0, (loserRating - winnerRating) / 400.0));

        // PRD: score_ratio sempre in [0.5, 1.0]. Negli sport a set i vincitori possono
        // avere meno unità totali dei perdenti (es. 6-4, 0-6, 7-6): clamp difensivo.
        double scoreRatio = Math.Clamp((double)winnerUnits / (totalTeam1 + totalTeam2), 0.5, 1.0);
        double effectiveResult = 0.5 + (scoreRatio - 0.5) * Amplifier;
        double margin = effectiveResult - expectedWin;

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

        int DeltaFor(Guid playerId, bool isWinner)
        {
            var delta = (int)Math.Round(kByPlayer[playerId] * margin);
            return isWinner ? delta : -delta;
        }

        match.DeltaTeam1Player1 = DeltaFor(match.Team1Player1Id, team1Won);
        match.DeltaTeam1Player2 = DeltaFor(match.Team1Player2Id, team1Won);
        match.DeltaTeam2Player1 = DeltaFor(match.Team2Player1Id, !team1Won);
        match.DeltaTeam2Player2 = DeltaFor(match.Team2Player2Id, !team1Won);

        memberships[match.Team1Player1Id].Rating += match.DeltaTeam1Player1.Value;
        memberships[match.Team1Player2Id].Rating += match.DeltaTeam1Player2.Value;
        memberships[match.Team2Player1Id].Rating += match.DeltaTeam2Player1.Value;
        memberships[match.Team2Player2Id].Rating += match.DeltaTeam2Player2.Value;

        // SaveChangesAsync è responsabilità del caller: stessa transazione della conferma
    }
}
