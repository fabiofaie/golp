using System.Security.Claims;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class CircleEndpoints
{
    public static IEndpointRouteBuilder MapCircleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/sports", GetSports);

        var circles = app.MapGroup("/circles").RequireAuthorization();
        circles.MapGet("/", GetAllCirclesAsync);
        circles.MapPost("/", CreateCircleAsync);
        circles.MapGet("/me", GetMyCirclesAsync);
        circles.MapPost("/{id:guid}/join", JoinCircleAsync);
        circles.MapGet("/{id:guid}/members", GetCircleMembersAsync);

        return app;
    }

    // GET /sports — public
    private static IResult GetSports() =>
        Results.Ok(SportsConfig.GetAll());

    // GET /circles — lista tutti i circoli con flag isAlreadyMember per l'utente corrente
    private static async Task<IResult> GetAllCirclesAsync(
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circles = await db.Circles
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Sport,
                MemberCount     = db.CircleMemberships.Count(m => m.CircleId == c.Id),
                IsAlreadyMember = db.CircleMemberships.Any(m => m.CircleId == c.Id && m.UserId == userId),
            })
            .ToListAsync();

        return Results.Ok(circles.Select(c => new
        {
            id              = c.Id,
            name            = c.Name,
            sport           = c.Sport,
            memberCount     = c.MemberCount,
            isAlreadyMember = c.IsAlreadyMember,
        }));
    }

    // POST /circles — auth required
    private static async Task<IResult> CreateCircleAsync(
        CreateCircleRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { error = "Il nome è obbligatorio" });

        if (req.Name.Length > 100)
            return Results.BadRequest(new { error = "Il nome non può superare i 100 caratteri" });

        var sportConfig = SportsConfig.GetBySport(req.Sport);
        if (sportConfig == null)
            return Results.BadRequest(new { error = "Sport non valido" });

        var exists = await db.Circles.AnyAsync(c => c.OwnerId == userId && c.Name == req.Name.Trim());
        if (exists)
            return Results.Conflict(new { error = "Hai già un circolo con questo nome" });

        var circle = new Circle
        {
            OwnerId   = userId,
            Name      = req.Name.Trim(),
            Sport     = sportConfig.Sport,
            PointUnit = sportConfig.PointUnit,
            Sets      = sportConfig.Sets,
            TeamSize  = sportConfig.TeamSize,
        };

        var membership = new CircleMembership
        {
            CircleId = circle.Id,
            UserId   = userId,
            Rating   = 1000,
        };

        db.Circles.Add(circle);
        db.CircleMemberships.Add(membership);
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            id          = circle.Id,
            name        = circle.Name,
            sport       = circle.Sport,
            pointUnit   = circle.PointUnit,
            sets        = circle.Sets,
            teamSize    = circle.TeamSize,
            joinCode    = circle.JoinCode,
            memberCount = 1,
        });
    }

    // GET /circles/me — auth required
    private static async Task<IResult> GetMyCirclesAsync(
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var memberships = await db.CircleMemberships
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.CircleId,
                CircleName  = m.Circle.Name,
                CircleSport = m.Circle.Sport,
                MyRating    = m.Rating,
                MemberCount = db.CircleMemberships.Count(x => x.CircleId == m.CircleId),
                MyRank      = db.CircleMemberships.Count(x => x.CircleId == m.CircleId && x.Rating > m.Rating) + 1,
            })
            .ToListAsync();

        return Results.Ok(memberships.Select(m => new
        {
            id          = m.CircleId,
            name        = m.CircleName,
            sport       = m.CircleSport,
            memberCount = m.MemberCount,
            myRating    = m.MyRating,
            myRank      = m.MyRank,
        }));
    }

    // POST /circles/{id}/join — iscrizione libera a circolo pubblico
    private static async Task<IResult> JoinCircleAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circle = await db.Circles.FindAsync(id);
        if (circle == null)
            return Results.NotFound(new { error = "Circolo non trovato" });

        if (circle.IsPrivate)
            return Results.Json(new { error = "Il circolo è privato" }, statusCode: 403);

        var alreadyMember = await db.CircleMemberships
            .AnyAsync(m => m.CircleId == id && m.UserId == userId);
        if (alreadyMember)
            return Results.Conflict(new { error = "Sei già membro di questo circolo" });

        db.CircleMemberships.Add(new CircleMembership
        {
            CircleId = id,
            UserId   = userId,
            Rating   = 1000,
        });
        await db.SaveChangesAsync();

        return Results.Ok(new { circleId = id, myRating = 1000 });
    }

    // GET /circles/{id}/members — lista membri ordinata per rating
    private static async Task<IResult> GetCircleMembersAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circleExists = await db.Circles.AnyAsync(c => c.Id == id);
        if (!circleExists)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var members = await db.CircleMemberships
            .Where(m => m.CircleId == id)
            .OrderByDescending(m => m.Rating)
            .Select(m => new
            {
                m.UserId,
                Name   = m.User.Name,
                m.Rating,
            })
            .ToListAsync();

        return Results.Ok(members.Select((m, i) => new
        {
            userId = m.UserId,
            name   = m.Name,
            rating = m.Rating,
            rank   = i + 1,
        }));
    }
}

record CreateCircleRequest(string Name, string Sport);
