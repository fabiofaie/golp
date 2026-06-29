using System.Security.Claims;
using Golp.Api.Data;
using Golp.Api.Services;
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
        AppDbContext db,
        IAwardsCalculator calculator)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out _))
            return Results.Unauthorized();

        var circleExists = await db.Circles.AnyAsync(c => c.Id == circleId);
        if (!circleExists)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var now = DateTimeOffset.UtcNow;
        var currentMonth = await calculator.ComputePeriodAsync(circleId, "month", now.Year, now.Month);
        var currentYear  = await calculator.ComputePeriodAsync(circleId, "year",  now.Year, null);

        return Results.Ok(new
        {
            currentMonth = ToResponse(currentMonth),
            currentYear  = ToResponse(currentYear),
        });
    }

    private static object ToResponse(AwardPeriodResult r) => new
    {
        period = r.Period,
        winner = r.Winner == null ? null : (object)new
        {
            userId        = r.Winner.UserId,
            name          = r.Winner.Name,
            netGain       = r.Winner.NetGain,
            matchesPlayed = r.Winner.MatchesPlayed,
        },
    };
}
