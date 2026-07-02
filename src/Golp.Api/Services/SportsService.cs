using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

public class SportsService(AppDbContext db) : ISportsService
{
    public async Task<IReadOnlyList<SportDto>> GetAllAsync() =>
        await db.Sports
            .Where(s => s.IsActive)
            .Select(s => new SportDto(s.Key, s.DisplayName, s.PointUnit, s.Sets, s.TeamSize, s.SetWeight, s.AllowsSingles))
            .ToListAsync();

    public async Task<SportDto?> GetBySportAsync(string sport) =>
        await db.Sports
            .Where(s => s.IsActive && s.Key == sport)
            .Select(s => new SportDto(s.Key, s.DisplayName, s.PointUnit, s.Sets, s.TeamSize, s.SetWeight, s.AllowsSingles))
            .FirstOrDefaultAsync();
}
