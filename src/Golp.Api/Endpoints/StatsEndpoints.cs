using System.Security.Claims;
using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class StatsEndpoints
{
    public static IEndpointRouteBuilder MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var stats = app.MapGroup("/circles/{circleId:guid}/stats").RequireAuthorization();
        stats.MapGet("/me", GetMyStatsAsync);
        return app;
    }

    private static async Task<IResult> GetMyStatsAsync(
        Guid circleId,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circleExists = await db.Circles.AnyAsync(c => c.Id == circleId);
        if (!circleExists)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var isMember = await db.CircleMemberships
            .AnyAsync(m => m.CircleId == circleId && m.UserId == userId);
        if (!isMember)
            return Results.Json(new { error = "Non sei membro del circolo" }, statusCode: 403);

        var asTeam1 = await db.Matches
            .Where(m => m.CircleId == circleId && m.Status == "confirmed"
                     && (m.Team1Player1Id == userId || m.Team1Player2Id == userId))
            .ToListAsync();
        var asTeam2 = await db.Matches
            .Where(m => m.CircleId == circleId && m.Status == "confirmed"
                     && (m.Team2Player1Id == userId || m.Team2Player2Id == userId))
            .ToListAsync();

        var asTeam1Ids = new HashSet<Guid>(asTeam1.Select(m => m.Id));
        var allMatches = asTeam1.Concat(asTeam2).ToList();

        var partnerStats = new Dictionary<Guid, (int Total, int Wins)>();
        var opponentStats = new Dictionary<Guid, (int Total, int Wins)>();

        foreach (var m in allMatches)
        {
            bool iAmTeam1 = asTeam1Ids.Contains(m.Id);
            bool iWon = (iAmTeam1 && m.WinnerTeam == 1) || (!iAmTeam1 && m.WinnerTeam == 2);

            var partner = iAmTeam1
                ? (m.Team1Player1Id == userId ? m.Team1Player2Id : m.Team1Player1Id)
                : (m.Team2Player1Id == userId ? m.Team2Player2Id : m.Team2Player1Id);

            Guid[] opponents = iAmTeam1
                ? [m.Team2Player1Id, m.Team2Player2Id]
                : [m.Team1Player1Id, m.Team1Player2Id];

            if (!partnerStats.TryGetValue(partner, out var ps))
                ps = (0, 0);
            partnerStats[partner] = (ps.Total + 1, ps.Wins + (iWon ? 1 : 0));

            foreach (var opp in opponents)
            {
                if (!opponentStats.TryGetValue(opp, out var os))
                    os = (0, 0);
                opponentStats[opp] = (os.Total + 1, os.Wins + (iWon ? 1 : 0));
            }
        }

        const int minGames = 3;

        var eligiblePartners = partnerStats
            .Where(kvp => kvp.Value.Total >= minGames)
            .Select(kvp => new
            {
                UserId = kvp.Key,
                WinRate = (double)kvp.Value.Wins / kvp.Value.Total,
                GamesTogether = kvp.Value.Total,
            })
            .OrderByDescending(x => x.WinRate)
            .ThenByDescending(x => x.GamesTogether)
            .ThenBy(x => x.UserId)
            .ToList();

        var eligibleOpponents = opponentStats
            .Where(kvp => kvp.Value.Total >= minGames)
            .Select(kvp => new
            {
                UserId = kvp.Key,
                WinRate = (double)kvp.Value.Wins / kvp.Value.Total,
                GamesAgainst = kvp.Value.Total,
            })
            .OrderBy(x => x.WinRate)
            .ThenByDescending(x => x.GamesAgainst)
            .ThenBy(x => x.UserId)
            .ToList();

        var allUserIds = eligiblePartners.Select(x => x.UserId)
            .Concat(eligibleOpponents.Select(x => x.UserId))
            .Distinct()
            .ToList();

        var names = allUserIds.Count > 0
            ? await db.Users
                .Where(u => allUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name)
            : new Dictionary<Guid, string>();

        object? bestPartner = null;
        if (eligiblePartners.Count > 0)
        {
            var bp = eligiblePartners[0];
            bestPartner = new
            {
                userId = bp.UserId,
                name = names.GetValueOrDefault(bp.UserId, ""),
                winRate = bp.WinRate,
                gamesTogether = bp.GamesTogether,
            };
        }

        object? toughestOpponent = null;
        if (eligibleOpponents.Count > 0)
        {
            var to = eligibleOpponents[0];
            toughestOpponent = new
            {
                userId = to.UserId,
                name = names.GetValueOrDefault(to.UserId, ""),
                winRate = to.WinRate,
                gamesAgainst = to.GamesAgainst,
            };
        }

        return Results.Ok(new { bestPartner, toughestOpponent });
    }
}
