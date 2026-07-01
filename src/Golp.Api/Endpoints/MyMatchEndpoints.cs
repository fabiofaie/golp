using System.Security.Claims;
using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class MyMatchEndpoints
{
    public static IEndpointRouteBuilder MapMyMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var mine = app.MapGroup("/match/mine").RequireAuthorization();
        mine.MapGet("/", GetMyMatchesAsync);
        return app;
    }

    // ─── GET /match/mine ─────────────────────────────────────────────────────

    private static async Task<IResult> GetMyMatchesAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        int page = 1,
        int pageSize = 20,
        string? status = null)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var query = db.Matches
            .Include(m => m.Sets)
            .Include(m => m.Circle)
            .Where(m =>
                m.Team1Player1Id == userId ||
                m.Team1Player2Id == userId ||
                m.Team2Player1Id == userId ||
                m.Team2Player2Id == userId);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(m => m.Status == status);

        var totalCount = await query.CountAsync();

        var matches = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var matchIds = matches.Select(m => m.Id).ToList();

        var confirmedByUser = await db.MatchConfirmations
            .Where(c => matchIds.Contains(c.MatchId) && c.UserId == userId)
            .Select(c => c.MatchId)
            .ToHashSetAsync();

        var items = matches.Select(m =>
        {
            int myTeam;
            int? myDelta = null;

            if (m.Team1Player1Id == userId || m.Team1Player2Id == userId)
            {
                myTeam = 1;
                if (m.Status == "confirmed")
                    myDelta = m.Team1Player1Id == userId ? m.DeltaTeam1Player1 : m.DeltaTeam1Player2;
            }
            else
            {
                myTeam = 2;
                if (m.Status == "confirmed")
                    myDelta = m.Team2Player1Id == userId ? m.DeltaTeam2Player1 : m.DeltaTeam2Player2;
            }

            return new
            {
                matchId               = m.Id,
                circleId              = m.CircleId,
                circleName            = m.Circle.Name,
                sport                 = m.Circle.Sport,
                createdAt             = m.CreatedAt,
                status                = m.Status,
                winnerTeam            = m.WinnerTeam,
                myTeam,
                sets                  = m.Sets.OrderBy(s => s.SetNumber).Select(s => new
                {
                    team1Score = s.Team1Score,
                    team2Score = s.Team2Score
                }),
                myDelta,
                hasCurrentUserConfirmed = confirmedByUser.Contains(m.Id),
            };
        });

        return Results.Ok(new
        {
            totalCount,
            page,
            pageSize,
            items,
        });
    }
}
