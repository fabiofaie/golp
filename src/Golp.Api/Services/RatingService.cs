using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

/// <summary>
/// ELO adattato per squadre (PRD §Algoritmo di ranking):
///   team_rating      = media dei rating dei membri (singolo: rating del giocatore)
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
    /// Calcola i delta ELO per i giocatori dato il risultato di una partita.
    /// Logica pura, senza accesso al DB — riusabile dal simulatore.
    /// </summary>
    /// <param name="team1Avg">Rating medio (o singolo) squadra 1</param>
    /// <param name="team2Avg">Rating medio (o singolo) squadra 2</param>
    /// <param name="kValues">K per ogni giocatore: [t1p1, t1p2, t2p1, t2p2] o [t1p1, t2p1] per singolo</param>
    /// <param name="team1Won">true se ha vinto squadra 1</param>
    /// <param name="scoreRatio">score_ratio già clampato in [0.5, 1.0] per il team vincente</param>
    /// <returns>delta per ogni giocatore in kValues — positivo se vincitore, negativo se perdente</returns>
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
            // US-034: un margine reale (la partita ha un vincitore) non deve mai arrotondare a 0.
            // Il segno segue margin, non isWinner: un super-favorito che vince di poco può avere margin < 0.
            if (delta == 0 && margin != 0)
                delta = margin > 0 ? 1 : -1;
            return isWinner ? delta : -delta;
        }

        int half = kValues.Length / 2;
        var result = new int[kValues.Length];
        for (int i = 0; i < half; i++)
            result[i] = DeltaFor(kValues[i], team1Won);
        for (int i = half; i < kValues.Length; i++)
            result[i] = DeltaFor(kValues[i], !team1Won);
        return result;
    }

    public async Task<IReadOnlyList<(Guid UserId, int NewPosition)>> CalculateAndApplyAsync(Guid matchId, AppDbContext db)
    {
        var match = await db.Matches
            .Include(m => m.Sets)
            .SingleOrDefaultAsync(m => m.Id == matchId);

        if (match == null || match.Status != "confirmed")
            return [];

        var circle = await db.Circles.FindAsync(match.CircleId);

        // Idempotenza: delta già presenti = match già processato
        if (match.DeltaTeam1Player1 != null)
            return [];

        int totalTeam1 = match.Sets.Sum(s => s.Team1Score);
        int totalTeam2 = match.Sets.Sum(s => s.Team2Score);
        if (totalTeam1 + totalTeam2 == 0)
            return [];

        bool team1Won   = match.WinnerTeam == 1;
        int winnerUnits = team1Won ? totalTeam1 : totalTeam2;

        var circleSport = circle?.Sport ?? "";
        var sport     = await db.Sports.FirstOrDefaultAsync(s => s.IsActive && s.Key == circleSport);
        bool useBlended = (circle?.Sets == true) && (sport?.SetWeight > 0);

        double scoreRatio = ComputeScoreRatio(match, useBlended, sport, team1Won, totalTeam1, totalTeam2, winnerUnits);

        if (match.IsSingles)
            return await ApplySinglesAsync(match, db, team1Won, scoreRatio);

        return await ApplyDoublesAsync(match, db, team1Won, scoreRatio);
    }

    private static double ComputeScoreRatio(Match match, bool useBlended, Sport? sport, bool team1Won, int totalTeam1, int totalTeam2, int winnerUnits)
    {
        if (useBlended)
        {
            int setsWonByWinner = match.Sets.Count(s =>
                team1Won ? s.Team1Score > s.Team2Score : s.Team2Score > s.Team1Score);
            int totalSets = match.Sets.Count(s => s.Team1Score != s.Team2Score);
            double setRatio  = totalSets > 0 ? (double)setsWonByWinner / totalSets : 0.5;
            double gameRatio = (double)winnerUnits / (totalTeam1 + totalTeam2);

            // US-034: set pari (vincitore deciso dai game) → margine solo da game, niente contributo set
            bool setsTied = setsWonByWinner == totalSets - setsWonByWinner;
            // US-034: game pari (vincitore deciso dai set) → margine solo da set, niente contributo game
            bool gamesTied = totalTeam1 == totalTeam2;

            double effectiveSetWeight = setsTied ? 0.0 : gamesTied ? 1.0 : sport!.SetWeight;
            return Math.Clamp(effectiveSetWeight * setRatio + (1 - effectiveSetWeight) * gameRatio, 0.5, 1.0);
        }
        else
        {
            // PRD: score_ratio in [0.5, 1.0]. Clamp difensivo per sport senza set o quando
            // il vincitore ha meno game totali dei perdenti (es. 6-4, 0-6, 7-6).
            return Math.Clamp((double)winnerUnits / (totalTeam1 + totalTeam2), 0.5, 1.0);
        }
    }

    private async Task<IReadOnlyList<(Guid UserId, int NewPosition)>> ApplySinglesAsync(
        Match match, AppDbContext db, bool team1Won, double scoreRatio)
    {
        var p1 = match.Team1Player1Id;
        var p2 = match.Team2Player1Id;

        var memberships = await db.CircleMemberships
            .Where(m => m.CircleId == match.CircleId && (m.UserId == p1 || m.UserId == p2))
            .ToDictionaryAsync(m => m.UserId);

        if (memberships.Count != 2)
            return [];

        var k1 = await CountKAsync(db, match, p1);
        var k2 = await CountKAsync(db, match, p2);

        double r1 = memberships[p1].Rating;
        double r2 = memberships[p2].Rating;

        var deltas = ComputeDeltas(r1, r2, [k1, k2], team1Won, scoreRatio);

        match.DeltaTeam1Player1 = deltas[0];
        match.DeltaTeam2Player1 = deltas[1];

        var playerIds = new[] { p1, p2 };

        var allRatings = await db.CircleMemberships
            .Where(m => m.CircleId == match.CircleId)
            .Select(m => new { m.UserId, m.Rating })
            .ToListAsync();
        var prePositions = allRatings
            .OrderByDescending(m => m.Rating)
            .Select((m, i) => (m.UserId, Position: i + 1))
            .ToDictionary(x => x.UserId, x => x.Position);

        memberships[p1].Rating += match.DeltaTeam1Player1.Value;
        memberships[p2].Rating += match.DeltaTeam2Player1.Value;

        var postRatings = allRatings
            .Select(m => playerIds.Contains(m.UserId)
                ? new { m.UserId, memberships[m.UserId].Rating }
                : m)
            .OrderByDescending(m => m.Rating)
            .Select((m, i) => (m.UserId, Position: i + 1))
            .ToDictionary(x => x.UserId, x => x.Position);

        int postPositions(Guid id) => postRatings.GetValueOrDefault(id, int.MaxValue);

        return playerIds
            .Where(id => postPositions(id) < prePositions.GetValueOrDefault(id, int.MaxValue))
            .Select(id => (UserId: id, NewPosition: postPositions(id)))
            .ToList();
    }

    private async Task<IReadOnlyList<(Guid UserId, int NewPosition)>> ApplyDoublesAsync(
        Match match, AppDbContext db, bool team1Won, double scoreRatio)
    {
        var playerIds = new[]
        {
            match.Team1Player1Id, match.Team1Player2Id!.Value,
            match.Team2Player1Id, match.Team2Player2Id!.Value,
        };

        var memberships = await db.CircleMemberships
            .Where(m => m.CircleId == match.CircleId && playerIds.Contains(m.UserId))
            .ToDictionaryAsync(m => m.UserId);

        if (memberships.Count != 4)
            return []; // dati incoerenti: nessun aggiornamento parziale

        double team1Rating = (memberships[match.Team1Player1Id].Rating
                            + memberships[match.Team1Player2Id!.Value].Rating) / 2.0;
        double team2Rating = (memberships[match.Team2Player1Id].Rating
                            + memberships[match.Team2Player2Id!.Value].Rating) / 2.0;

        var k1 = await CountKAsync(db, match, match.Team1Player1Id);
        var k2 = await CountKAsync(db, match, match.Team1Player2Id!.Value);
        var k3 = await CountKAsync(db, match, match.Team2Player1Id);
        var k4 = await CountKAsync(db, match, match.Team2Player2Id!.Value);
        var kValues = new[] { k1, k2, k3, k4 };

        var deltas = ComputeDeltas(team1Rating, team2Rating, kValues, team1Won, scoreRatio);

        match.DeltaTeam1Player1 = deltas[0];
        match.DeltaTeam1Player2 = deltas[1];
        match.DeltaTeam2Player1 = deltas[2];
        match.DeltaTeam2Player2 = deltas[3];

        var allRatings = await db.CircleMemberships
            .Where(m => m.CircleId == match.CircleId)
            .Select(m => new { m.UserId, m.Rating })
            .ToListAsync();
        var prePositions = allRatings
            .OrderByDescending(m => m.Rating)
            .Select((m, i) => (m.UserId, Position: i + 1))
            .ToDictionary(x => x.UserId, x => x.Position);

        memberships[match.Team1Player1Id].Rating    += match.DeltaTeam1Player1.Value;
        memberships[match.Team1Player2Id!.Value].Rating += match.DeltaTeam1Player2.Value;
        memberships[match.Team2Player1Id].Rating    += match.DeltaTeam2Player1.Value;
        memberships[match.Team2Player2Id!.Value].Rating += match.DeltaTeam2Player2.Value;

        var postRatings = allRatings
            .Select(m => playerIds.Contains(m.UserId)
                ? new { m.UserId, memberships[m.UserId].Rating }
                : m)
            .OrderByDescending(m => m.Rating)
            .Select((m, i) => (m.UserId, Position: i + 1))
            .ToDictionary(x => x.UserId, x => x.Position);

        int postPositions(Guid id) => postRatings.GetValueOrDefault(id, int.MaxValue);

        return playerIds
            .Where(id => postPositions(id) < prePositions.GetValueOrDefault(id, int.MaxValue))
            .Select(id => (UserId: id, NewPosition: postPositions(id)))
            .ToList();
    }

    private static async Task<int> CountKAsync(AppDbContext db, Match match, Guid playerId)
    {
        var count = await db.Matches.CountAsync(m =>
            m.CircleId == match.CircleId
            && m.Id != match.Id
            && m.Status == "confirmed"
            && (m.Team1Player1Id == playerId || m.Team1Player2Id == playerId
             || m.Team2Player1Id == playerId || m.Team2Player2Id == playerId));
        return count < ColdStartMatches ? KColdStart : KDefault;
    }

    // kept for compatibility — unused internally
    private static async Task<int> CountConfirmedMatches(AppDbContext db, Match match, Guid playerId)
        => await db.Matches.CountAsync(m =>
            m.CircleId == match.CircleId
            && m.Id != match.Id
            && m.Status == "confirmed"
            && (m.Team1Player1Id == playerId || m.Team1Player2Id == playerId
             || m.Team2Player1Id == playerId || m.Team2Player2Id == playerId));
}
