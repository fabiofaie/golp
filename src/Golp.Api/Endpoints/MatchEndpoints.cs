using System.Security.Claims;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var matches = app.MapGroup("/circles/{circleId:guid}/matches").RequireAuthorization();
        matches.MapPost("/", CreateMatchAsync);
        return app;
    }

    private static async Task<IResult> CreateMatchAsync(
        Guid circleId,
        CreateMatchRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circle = await db.Circles.FindAsync(circleId);
        if (circle == null)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var memberIds = await db.CircleMemberships
            .Where(m => m.CircleId == circleId)
            .Select(m => m.UserId)
            .ToHashSetAsync();

        if (!memberIds.Contains(userId))
            return Results.Json(new { error = "Non sei membro del circolo" }, statusCode: 403);

        if (req.Team1 == null || req.Team1.Length != 2)
            return Results.BadRequest(new { error = "Team1 deve avere esattamente 2 giocatori" });

        if (req.Team2 == null || req.Team2.Length != 2)
            return Results.BadRequest(new { error = "Team2 deve avere esattamente 2 giocatori" });

        var allPlayers = req.Team1.Concat(req.Team2).ToArray();

        if (allPlayers.Distinct().Count() != 4)
            return Results.BadRequest(new { error = "I 4 giocatori devono essere tutti distinti" });

        foreach (var playerId in allPlayers)
        {
            if (!memberIds.Contains(playerId))
                return Results.BadRequest(new { error = $"Il giocatore {playerId} non è membro del circolo" });
        }

        var creatorInTeam = req.Team1.Contains(userId) || req.Team2.Contains(userId);
        if (!creatorInTeam)
            return Results.BadRequest(new { error = "L'inseritore deve essere uno dei 4 giocatori" });

        if (req.Sets == null || req.Sets.Length == 0)
            return Results.BadRequest(new { error = "Il punteggio è obbligatorio" });

        if (!circle.Sets && req.Sets.Length != 1)
            return Results.BadRequest(new { error = "Gli sport senza set richiedono esattamente un punteggio" });

        int team1Wins = req.Sets.Count(s => s.Team1 > s.Team2);
        int team2Wins = req.Sets.Count(s => s.Team2 > s.Team1);

        int winnerTeam;
        if (circle.Sets)
        {
            if (team1Wins == team2Wins)
                return Results.BadRequest(new { error = "La partita deve avere un vincitore (set pari non ammessi)" });
            winnerTeam = team1Wins > team2Wins ? 1 : 2;
        }
        else
        {
            var score = req.Sets[0];
            if (score.Team1 == score.Team2)
                return Results.BadRequest(new { error = "La partita deve avere un vincitore (pareggio non ammesso)" });
            winnerTeam = score.Team1 > score.Team2 ? 1 : 2;
        }

        var match = new Match
        {
            CircleId       = circleId,
            CreatedById    = userId,
            WinnerTeam     = winnerTeam,
            Team1Player1Id = req.Team1[0],
            Team1Player2Id = req.Team1[1],
            Team2Player1Id = req.Team2[0],
            Team2Player2Id = req.Team2[1],
        };

        var sets = req.Sets.Select((s, i) => new MatchSet
        {
            MatchId    = match.Id,
            SetNumber  = i + 1,
            Team1Score = s.Team1,
            Team2Score = s.Team2,
        }).ToList();

        db.Matches.Add(match);
        db.MatchSets.AddRange(sets);
        await db.SaveChangesAsync();

        return Results.Created($"/circles/{circleId}/matches/{match.Id}", new
        {
            id         = match.Id,
            circleId   = match.CircleId,
            status     = match.Status,
            winnerTeam = match.WinnerTeam,
            createdAt  = match.CreatedAt,
        });
    }
}

record CreateMatchRequest(Guid[] Team1, Guid[] Team2, SetScoreDto[] Sets);
record SetScoreDto(int Team1, int Team2);
