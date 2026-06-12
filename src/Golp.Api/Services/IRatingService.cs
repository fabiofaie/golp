using Golp.Api.Data;

namespace Golp.Api.Services;

public interface IRatingService
{
    Task CalculateAndApplyAsync(Guid matchId, AppDbContext db);
}
