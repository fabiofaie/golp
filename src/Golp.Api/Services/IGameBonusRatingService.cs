using Golp.Api.Data;

namespace Golp.Api.Services;

public interface IGameBonusRatingService
{
    Task CalculateAndApplyAsync(Guid matchId, AppDbContext db);

    /// <summary>
    /// US-062: azzera `GameBonusWinnerPoints` (e i delta esposti) di tutte le partite `confirmed`
    /// del circolo e le rigioca in ordine cronologico. Necessario perché il bonus upset di ogni
    /// partita dipende dallo snapshot della finestra di punteggi al momento del calcolo — modificare
    /// il risultato di una partita intermedia richiede di ricostruire la sequenza da quel punto in poi,
    /// non solo ricalcolare la singola partita modificata (path-dependency analoga a RatingService).
    /// </summary>
    Task ResetAndReplayCircleAsync(Guid circleId, AppDbContext db);
}
