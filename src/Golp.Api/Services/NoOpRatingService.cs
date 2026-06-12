using Golp.Api.Data;

namespace Golp.Api.Services;

// Placeholder until US-007 implements the real ELO calculation.
public class NoOpRatingService : IRatingService
{
    public Task CalculateAndApplyAsync(Guid matchId, AppDbContext db) => Task.CompletedTask;
}
