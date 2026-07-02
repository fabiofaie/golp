using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

public class AwardsCalculator(AppDbContext db) : IAwardsCalculator
{
    public async Task<AwardPeriodResult> ComputePeriodAsync(Guid circleId, string periodType, int year, int? month)
    {
        var periodStart = month.HasValue
            ? new DateTimeOffset(year, month.Value, 1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var periodEnd = month.HasValue
            ? periodStart.AddMonths(1)
            : new DateTimeOffset(year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var periodLabel = month.HasValue ? $"{year}-{month.Value:D2}" : $"{year}";

        var matches = await db.Matches
            .Where(m => m.CircleId == circleId
                     && m.Status == "confirmed"
                     && m.CreatedAt >= periodStart
                     && m.CreatedAt < periodEnd)
            .ToListAsync();

        if (matches.Count == 0)
            return new AwardPeriodResult(periodLabel, null);

        var top = matches
            .SelectMany(m =>
            {
                var required = new[] {
                    (UserId: m.Team1Player1Id, Delta: m.DeltaTeam1Player1 ?? 0),
                    (UserId: m.Team2Player1Id, Delta: m.DeltaTeam2Player1 ?? 0),
                };
                if (m.IsSingles) return required;
                return required.Concat(new[] {
                    (UserId: m.Team1Player2Id!.Value, Delta: m.DeltaTeam1Player2 ?? 0),
                    (UserId: m.Team2Player2Id!.Value, Delta: m.DeltaTeam2Player2 ?? 0),
                });
            })
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId        = g.Key,
                NetGain       = g.Sum(x => x.Delta),
                MatchesPlayed = g.Count(),
            })
            .OrderByDescending(x => x.NetGain)
            .ThenByDescending(x => x.MatchesPlayed)
            .ThenBy(x => x.UserId)
            .First();

        var winnerName = await db.Users
            .Where(u => u.Id == top.UserId)
            .Select(u => u.Name)
            .FirstOrDefaultAsync() ?? "";

        return new AwardPeriodResult(
            periodLabel,
            new AwardWinner(top.UserId, winnerName, top.NetGain, top.MatchesPlayed));
    }
}
