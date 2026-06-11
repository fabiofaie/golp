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
        circles.MapPost("/", CreateCircleAsync);
        circles.MapGet("/me", GetMyCirclesAsync);

        return app;
    }

    // GET /sports — public
    private static IResult GetSports() =>
        Results.Ok(SportsConfig.GetAll());

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
}

record CreateCircleRequest(string Name, string Sport);
