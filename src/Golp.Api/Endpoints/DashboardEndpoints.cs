using System.Security.Claims;
using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

// US-070: aggrega in un'unica risposta i dati che la dashboard oggi richiede con più
// chiamate separate (circoli, dettaglio circolo attivo o aggregato, richieste urgenti).
public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/dashboard/summary", GetDashboardSummaryAsync).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> GetDashboardSummaryAsync(
        ClaimsPrincipal user,
        AppDbContext db,
        string? circleId = null)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        // AC4: anche la lista circoli è avvolta in try/catch — è la prima query eseguita e,
        // senza fallback, un suo errore abbatterebbe l'intera risposta anche se le altre
        // sezioni sarebbero state recuperabili.
        List<CircleRow> circles;
        try
        {
            circles = await GetCirclesAsync(db, userId);
        }
        catch
        {
            circles = new List<CircleRow>();
        }

        // circleId assente/"all"/non valido/non-membro -> modalità aggregata (US-067),
        // nessun errore bloccante (AC4): il fallback è sempre la vista "tutti i circoli".
        Guid? resolvedCircleId = null;
        if (!string.IsNullOrEmpty(circleId) && circleId != "all"
            && Guid.TryParse(circleId, out var parsed)
            && circles.Any(c => c.Id == parsed))
        {
            resolvedCircleId = parsed;
        }

        object? activeCircle = null;
        object? aggregate = null;

        if (resolvedCircleId.HasValue)
        {
            activeCircle = await TryBuildActiveCircleAsync(db, userId, resolvedCircleId.Value, circles);
        }
        else
        {
            aggregate = await TryBuildAggregateAsync(db, userId, circles);
        }

        var urgentMatches = await TryGetUrgentMatchesAsync(db, userId);

        return Results.Ok(new
        {
            circles = circles.Select(c => new
            {
                id = c.Id,
                name = c.Name,
                sport = c.Sport,
                sets = c.Sets,
                pointUnit = c.PointUnit,
                ownerId = c.OwnerId,
                memberCount = c.MemberCount,
                myRating = c.MyRating,
                myRank = c.MyRank,
                joinedAt = c.JoinedAt,
            }),
            activeCircle,
            aggregate,
            urgentMatches,
        });
    }

    private sealed record CircleRow(
        Guid Id, string Name, string Sport, bool Sets, string PointUnit, Guid OwnerId,
        int MemberCount, int MyRating, int MyRank, DateTime JoinedAt);

    private static async Task<List<CircleRow>> GetCirclesAsync(AppDbContext db, Guid userId)
    {
        var memberships = await db.CircleMemberships
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.CircleId,
                CircleName = m.Circle.Name,
                CircleSport = m.Circle.Sport,
                CircleSets = m.Circle.Sets,
                CirclePointUnit = m.Circle.PointUnit,
                CircleOwnerId = m.Circle.OwnerId,
                MyRating = m.Rating,
                MemberCount = db.CircleMemberships.Count(x => x.CircleId == m.CircleId),
                MyRank = db.CircleMemberships.Count(x => x.CircleId == m.CircleId && x.Rating > m.Rating) + 1,
                m.JoinedAt,
            })
            .ToListAsync();

        return memberships
            .Select(m => new CircleRow(m.CircleId, m.CircleName, m.CircleSport, m.CircleSets, m.CirclePointUnit,
                m.CircleOwnerId, m.MemberCount, m.MyRating, m.MyRank, m.JoinedAt))
            .ToList();
    }

    // AC4: un errore nel calcolo del dettaglio circolo attivo non deve abbattere l'intera risposta.
    private static async Task<object?> TryBuildActiveCircleAsync(AppDbContext db, Guid userId, Guid circleId, List<CircleRow> circles)
    {
        try
        {
            var circle = circles.FirstOrDefault(c => c.Id == circleId);
            if (circle == null) return null;

            var confirmedMatches = await db.Matches
                .Where(m => m.CircleId == circleId && m.Status == "confirmed")
                .OrderByDescending(m => m.CreatedAt)
                .Take(20)
                .ToListAsync();

            var playerIds = confirmedMatches
                .SelectMany(m => new[] { m.Team1Player1Id, m.Team2Player1Id }
                    .Concat(new[] { m.Team1Player2Id, m.Team2Player2Id }.Where(id => id.HasValue).Select(id => id!.Value)))
                .Distinct()
                .ToHashSet();

            var userInfos = playerIds.Count > 0
                ? await db.Users.Where(u => playerIds.Contains(u.Id)).Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name)
                : new Dictionary<Guid, string>();

            var confirmedMatchIds = confirmedMatches.Select(m => m.Id).ToList();
            var confirmationCounts = await db.MatchConfirmations
                .Where(c => confirmedMatchIds.Contains(c.MatchId))
                .GroupBy(c => c.MatchId)
                .Select(g => new { MatchId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MatchId, x => x.Count);
            var userConfirmedSet = await db.MatchConfirmations
                .Where(c => confirmedMatchIds.Contains(c.MatchId) && c.UserId == userId)
                .Select(c => c.MatchId)
                .ToHashSetAsync();

            var recentMatches = confirmedMatches.Select(m =>
            {
                int? myDelta = null;
                if (m.Team1Player1Id == userId) myDelta = m.DeltaTeam1Player1;
                else if (m.Team1Player2Id == userId) myDelta = m.DeltaTeam1Player2;
                else if (m.Team2Player1Id == userId) myDelta = m.DeltaTeam2Player1;
                else if (m.Team2Player2Id == userId) myDelta = m.DeltaTeam2Player2;

                return new
                {
                    id = m.Id,
                    status = m.Status,
                    winnerTeam = m.WinnerTeam,
                    createdAt = m.CreatedAt,
                    myDelta,
                    confirmationsCount = confirmationCounts.GetValueOrDefault(m.Id, 0),
                    hasCurrentUserConfirmed = userConfirmedSet.Contains(m.Id),
                    team1 = m.IsSingles
                        ? new[] { new { userId = m.Team1Player1Id, name = userInfos.GetValueOrDefault(m.Team1Player1Id, "") } }
                        : new[] { new { userId = m.Team1Player1Id, name = userInfos.GetValueOrDefault(m.Team1Player1Id, "") }, new { userId = m.Team1Player2Id!.Value, name = userInfos.GetValueOrDefault(m.Team1Player2Id.Value, "") } },
                    team2 = m.IsSingles
                        ? new[] { new { userId = m.Team2Player1Id, name = userInfos.GetValueOrDefault(m.Team2Player1Id, "") } }
                        : new[] { new { userId = m.Team2Player1Id, name = userInfos.GetValueOrDefault(m.Team2Player1Id, "") }, new { userId = m.Team2Player2Id!.Value, name = userInfos.GetValueOrDefault(m.Team2Player2Id.Value, "") } },
                };
            });

            return new
            {
                id = circle.Id,
                name = circle.Name,
                sport = circle.Sport,
                myRating = circle.MyRating,
                myRank = circle.MyRank,
                memberCount = circle.MemberCount,
                confirmedMatchesCount = confirmedMatches.Count,
                recentMatches,
            };
        }
        catch
        {
            return null;
        }
    }

    // AC4: un errore nel calcolo dell'aggregato "tutti i circoli" non deve abbattere l'intera risposta.
    private static async Task<object?> TryBuildAggregateAsync(AppDbContext db, Guid userId, List<CircleRow> circles)
    {
        try
        {
            var confirmedMatches = await db.Matches
                .Where(m => m.Status == "confirmed" &&
                    (m.Team1Player1Id == userId || m.Team1Player2Id == userId ||
                     m.Team2Player1Id == userId || m.Team2Player2Id == userId))
                .ToListAsync();

            int wins = confirmedMatches.Count(m =>
                (m.Team1Player1Id == userId || m.Team1Player2Id == userId) && m.WinnerTeam == 1 ||
                (m.Team2Player1Id == userId || m.Team2Player2Id == userId) && m.WinnerTeam == 2);

            int winRate = confirmedMatches.Count == 0 ? 0 : (int)Math.Round(100.0 * wins / confirmedMatches.Count);

            return new
            {
                circlesCount = circles.Count,
                confirmedMatchesCount = confirmedMatches.Count,
                winRate,
            };
        }
        catch
        {
            return null;
        }
    }

    // AC4: un errore nel calcolo delle richieste urgenti non deve abbattere l'intera risposta.
    private static async Task<List<object>> TryGetUrgentMatchesAsync(AppDbContext db, Guid userId)
    {
        try
        {
            var matches = await db.Matches
                .Where(m => (m.Status == "pending" || m.Status == "disputed") &&
                    (m.Team1Player1Id == userId || m.Team1Player2Id == userId ||
                     m.Team2Player1Id == userId || m.Team2Player2Id == userId))
                .Include(m => m.Circle)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            if (matches.Count == 0) return new List<object>();

            var matchIds = matches.Select(m => m.Id).ToList();

            var confirmationCounts = await db.MatchConfirmations
                .Where(c => matchIds.Contains(c.MatchId))
                .GroupBy(c => c.MatchId)
                .Select(g => new { MatchId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MatchId, x => x.Count);

            var userConfirmedSet = await db.MatchConfirmations
                .Where(c => matchIds.Contains(c.MatchId) && c.UserId == userId)
                .Select(c => c.MatchId)
                .ToHashSetAsync();

            // Un match "pending" già confermato dall'utente non richiede più azione da parte sua:
            // resta "urgente" solo finché aspetta la SUA conferma. I "disputed" restano sempre
            // visibili (la contestazione va vista/gestita indipendentemente da chi ha già confermato).
            matches = matches
                .Where(m => m.Status == "disputed" || !userConfirmedSet.Contains(m.Id))
                .ToList();
            if (matches.Count == 0) return new List<object>();

            var playerIds = matches
                .SelectMany(m => new[] { m.Team1Player1Id, m.Team2Player1Id }
                    .Concat(new[] { m.Team1Player2Id, m.Team2Player2Id }.Where(id => id.HasValue).Select(id => id!.Value)))
                .Distinct()
                .ToHashSet();
            var userInfos = playerIds.Count > 0
                ? await db.Users.Where(u => playerIds.Contains(u.Id)).Select(u => new { u.Id, u.Name }).ToDictionaryAsync(u => u.Id, u => u.Name)
                : new Dictionary<Guid, string>();

            return matches.Select(m =>
            {
                int myTeam = (m.Team1Player1Id == userId || m.Team1Player2Id == userId) ? 1 : 2;
                return (object)new
                {
                    matchId = m.Id,
                    circleId = m.CircleId,
                    circleName = m.Circle.Name,
                    sport = m.Circle.Sport,
                    createdAt = m.CreatedAt,
                    status = m.Status,
                    winnerTeam = m.WinnerTeam,
                    myTeam,
                    sets = Array.Empty<object>(),
                    myDelta = (int?)null,
                    confirmationsCount = confirmationCounts.GetValueOrDefault(m.Id, 0),
                    hasCurrentUserConfirmed = userConfirmedSet.Contains(m.Id),
                    team1 = m.IsSingles
                        ? new[] { new { userId = m.Team1Player1Id, name = userInfos.GetValueOrDefault(m.Team1Player1Id, "") } }
                        : new[] { new { userId = m.Team1Player1Id, name = userInfos.GetValueOrDefault(m.Team1Player1Id, "") }, new { userId = m.Team1Player2Id!.Value, name = userInfos.GetValueOrDefault(m.Team1Player2Id.Value, "") } },
                    team2 = m.IsSingles
                        ? new[] { new { userId = m.Team2Player1Id, name = userInfos.GetValueOrDefault(m.Team2Player1Id, "") } }
                        : new[] { new { userId = m.Team2Player1Id, name = userInfos.GetValueOrDefault(m.Team2Player1Id, "") }, new { userId = m.Team2Player2Id!.Value, name = userInfos.GetValueOrDefault(m.Team2Player2Id.Value, "") } },
                };
            }).ToList();
        }
        catch
        {
            return new List<object>();
        }
    }
}
