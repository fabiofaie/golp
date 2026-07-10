using Golp.Api.Data;

namespace Golp.Api.Services;

public interface IRatingService
{
    Task<IReadOnlyList<(Guid UserId, int NewPosition)>> CalculateAndApplyAsync(Guid matchId, AppDbContext db);

    /// <summary>
    /// US-061: azzera il rating di tutti i membri del circolo a 1000 e rigioca in ordine cronologico
    /// tutte le partite `confirmed` del circolo tranne <paramref name="excludeMatchId"/>. Necessario perché
    /// il calcolo ELO è sequenziale e path-dependent (rating e K-value di ogni partita dipendono dallo
    /// stato prodotto dalle precedenti) — cancellare una partita intermedia richiede di ricostruire
    /// l'intera storia del circolo, non solo compensare il delta della singola partita cancellata.
    /// </summary>
    Task ResetAndReplayCircleAsync(Guid circleId, Guid excludeMatchId, AppDbContext db);
}
