using Golp.Api.Data;

namespace Golp.Api.Services;

public interface IRatingService
{
    Task<IReadOnlyList<(Guid UserId, int NewPosition)>> CalculateAndApplyAsync(Guid matchId, AppDbContext db);
}
