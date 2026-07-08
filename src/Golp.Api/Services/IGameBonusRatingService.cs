using Golp.Api.Data;

namespace Golp.Api.Services;

public interface IGameBonusRatingService
{
    Task CalculateAndApplyAsync(Guid matchId, AppDbContext db);
}
