using System.Security.Claims;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class PushEndpoints
{
    public static IEndpointRouteBuilder MapPushEndpoints(this IEndpointRouteBuilder app)
    {
        var push = app.MapGroup("/api/push");
        push.MapPost("/token", RegisterTokenAsync).RequireAuthorization();
        push.MapDelete("/token", UnregisterTokenAsync).RequireAuthorization();
        push.MapGet("/vapid-public-key", GetVapidPublicKey);
        push.MapPost("/test", SendTestNotificationAsync).RequireAuthorization();
        return app;
    }

    // ─── POST /token ──────────────────────────────────────────────────────────

    private static async Task<IResult> RegisterTokenAsync(
        RegisterPushTokenRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Token))
            return Results.BadRequest(new { error = "Token obbligatorio" });

        var exists = await db.FcmTokens
            .AnyAsync(t => t.UserId == userId && t.Token == req.Token);

        if (!exists)
        {
            db.FcmTokens.Add(new FcmToken
            {
                UserId   = userId,
                Token    = req.Token,
                DeviceId = req.DeviceId ?? string.Empty,
            });
            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Race su richieste concorrenti: l'indice unico (UserId, Token)
                // ha già il record → idempotente, stesso esito del ramo exists
            }
        }

        return Results.NoContent();
    }

    // ─── DELETE /token ────────────────────────────────────────────────────────

    private static async Task<IResult> UnregisterTokenAsync(
        [FromBody] UnregisterPushTokenRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var tokens = await db.FcmTokens
            .Where(t => t.UserId == userId && t.Token == req.Token)
            .ToListAsync();

        if (tokens.Count > 0)
        {
            db.FcmTokens.RemoveRange(tokens);
            await db.SaveChangesAsync();
        }

        return Results.NoContent();
    }

    // ─── POST /test ───────────────────────────────────────────────────────────

    private static async Task<IResult> SendTestNotificationAsync(
        ClaimsPrincipal user,
        IPushNotificationService pushService)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var sent = await pushService.SendTestNotificationAsync(userId);
        return sent ? Results.NoContent() : Results.NotFound(new { error = "Nessun token push registrato" });
    }

    // ─── GET /vapid-public-key ────────────────────────────────────────────────

    private static IResult GetVapidPublicKey(IConfiguration config)
    {
        var key = config["Firebase:VapidPublicKey"];
        if (string.IsNullOrEmpty(key))
            return Results.NotFound(new { error = "VAPID key non configurata" });

        return Results.Ok(new { key });
    }
}

record RegisterPushTokenRequest(string Token, string? DeviceId);
record UnregisterPushTokenRequest(string Token);
