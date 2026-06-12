using System.Security.Claims;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class AwardsEndpoints
{
    public static IEndpointRouteBuilder MapAwardsEndpoints(this IEndpointRouteBuilder app)
    {
        var awards = app.MapGroup("/circles/{circleId:guid}/awards").RequireAuthorization();
        awards.MapGet("/", GetAwardsAsync);
        return app;
    }

    private static async Task<IResult> GetAwardsAsync(
        Guid circleId,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out _))
            return Results.Unauthorized();

        var circleExists = await db.Circles.AnyAsync(c => c.Id == circleId);
        if (!circleExists)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var now = DateTimeOffset.UtcNow;
        var currentMonth = await ComputePeriodAsync(db, circleId, "month", now.Year, now.Month);
        var currentYear  = await ComputePeriodAsync(db, circleId, "year",  now.Year, null);

        return Results.Ok(new { currentMonth, currentYear });
    }

    private static async Task<object> ComputePeriodAsync(
        AppDbContext db, Guid circleId, string periodType, int year, int? month)
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
            return new { period = periodLabel, winner = (object?)null };

        var top = matches
            .SelectMany(m => new[]
            {
                (UserId: m.Team1Player1Id, Delta: m.DeltaTeam1Player1 ?? 0),
                (UserId: m.Team1Player2Id, Delta: m.DeltaTeam1Player2 ?? 0),
                (UserId: m.Team2Player1Id, Delta: m.DeltaTeam2Player1 ?? 0),
                (UserId: m.Team2Player2Id, Delta: m.DeltaTeam2Player2 ?? 0),
            })
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                UserId       = g.Key,
                NetGain      = g.Sum(x => x.Delta),
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

        return new
        {
            period = periodLabel,
            winner = new
            {
                userId        = top.UserId,
                name          = winnerName,
                netGain       = top.NetGain,
                matchesPlayed = top.MatchesPlayed,
            },
        };
    }
}
