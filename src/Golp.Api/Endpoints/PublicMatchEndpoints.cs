using Golp.Api.Data;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class PublicMatchEndpoints
{
    public static IEndpointRouteBuilder MapPublicMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var pub = app.MapGroup("/m");
        pub.MapGet("/{token:guid}", GetPublicMatchAsync);
        pub.MapPost("/{token:guid}/confirm", ConfirmViaTokenAsync);
        pub.MapPost("/{token:guid}/dispute", DisputeViaTokenAsync);
        return app;
    }

    // ─── GET /m/{token} ──────────────────────────────────────────────────────

    private static async Task<IResult> GetPublicMatchAsync(Guid token, AppDbContext db)
    {
        var confirmToken = await db.MatchConfirmationTokens
            .FirstOrDefaultAsync(t => t.Token == token);

        if (confirmToken == null)
            return Results.NotFound(new { error = "Token non trovato" });

        if (confirmToken.ExpiresAt < DateTimeOffset.UtcNow)
            return Results.Json(new { error = "Token scaduto" }, statusCode: 410);

        var match = await db.Matches.FindAsync(confirmToken.MatchId);
        if (match == null)
            return Results.NotFound(new { error = "Partita non trovata" });

        var circle = await db.Circles.FindAsync(match.CircleId);
        var playerIds = new[] { match.Team1Player1Id, match.Team2Player1Id }
            .Concat(new[] { match.Team1Player2Id, match.Team2Player2Id }.Where(id => id.HasValue).Select(id => id!.Value))
            .ToArray();
        var playerInfos = await db.Users
            .Where(u => playerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.IsActivated })
            .ToDictionaryAsync(u => u.Id);

        var sets = await db.MatchSets
            .Where(s => s.MatchId == match.Id)
            .OrderBy(s => s.SetNumber)
            .Select(s => new { s.Team1Score, s.Team2Score })
            .ToListAsync();

        var confirmationsCount = await db.MatchConfirmations.CountAsync(c => c.MatchId == match.Id);
        var userHasConfirmed = await db.MatchConfirmations.AnyAsync(c => c.MatchId == match.Id && c.UserId == confirmToken.UserId);

        var tokenOwner = playerInfos.GetValueOrDefault(confirmToken.UserId);

        var matchPayload = new
        {
            id                 = match.Id,
            sport              = circle?.Sport ?? "",
            circleName         = circle?.Name ?? "",
            status             = match.Status,
            winnerTeam         = match.WinnerTeam,
            confirmationsCount,
            sets,
            team1 = match.IsSingles
                ? new[] { new { userId = match.Team1Player1Id, name = playerInfos.GetValueOrDefault(match.Team1Player1Id)?.Name ?? "", isActivated = playerInfos.GetValueOrDefault(match.Team1Player1Id)?.IsActivated ?? true } }
                : new[] { new { userId = match.Team1Player1Id, name = playerInfos.GetValueOrDefault(match.Team1Player1Id)?.Name ?? "", isActivated = playerInfos.GetValueOrDefault(match.Team1Player1Id)?.IsActivated ?? true }, new { userId = match.Team1Player2Id!.Value, name = playerInfos.GetValueOrDefault(match.Team1Player2Id.Value)?.Name ?? "", isActivated = playerInfos.GetValueOrDefault(match.Team1Player2Id.Value)?.IsActivated ?? true } },
            team2 = match.IsSingles
                ? new[] { new { userId = match.Team2Player1Id, name = playerInfos.GetValueOrDefault(match.Team2Player1Id)?.Name ?? "", isActivated = playerInfos.GetValueOrDefault(match.Team2Player1Id)?.IsActivated ?? true } }
                : new[] { new { userId = match.Team2Player1Id, name = playerInfos.GetValueOrDefault(match.Team2Player1Id)?.Name ?? "", isActivated = playerInfos.GetValueOrDefault(match.Team2Player1Id)?.IsActivated ?? true }, new { userId = match.Team2Player2Id!.Value, name = playerInfos.GetValueOrDefault(match.Team2Player2Id.Value)?.Name ?? "", isActivated = playerInfos.GetValueOrDefault(match.Team2Player2Id.Value)?.IsActivated ?? true } },
        };

        if (confirmToken.UsedAt != null)
        {
            return Results.Ok(new
            {
                tokenUsed = true,
                match     = matchPayload,
                token     = new { userId = confirmToken.UserId, userName = tokenOwner?.Name ?? "" },
            });
        }

        return Results.Ok(new
        {
            tokenUsed = false,
            match     = matchPayload,
            token     = new
            {
                valid           = true,
                userId          = confirmToken.UserId,
                userName        = tokenOwner?.Name ?? "",
                userHasConfirmed,
            },
        });
    }

    // ─── POST /m/{token}/confirm ──────────────────────────────────────────────

    private static async Task<IResult> ConfirmViaTokenAsync(
        Guid token,
        AppDbContext db,
        IRatingService ratingService,
        IPushNotificationService pushService)
    {
        var (confirmToken, match, validateResult) = await ValidateTokenAsync(token, db);
        if (validateResult != null) return validateResult;

        if (confirmToken!.UsedAt != null)
        {
            var count = await db.MatchConfirmations.CountAsync(c => c.MatchId == match!.Id);
            return Results.Ok(new { alreadyDone = true, status = match!.Status, confirmationsCount = count });
        }

        if (match!.Status is "confirmed" or "disputed")
            return Results.Conflict(new { error = $"La partita è già {match.Status}" });

        var (_, status, totalCount, improvements) = await MatchEndpoints.PrepareConfirmAsync(
            match.Id, confirmToken.UserId, match, db, ratingService);

        confirmToken.UsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); // salva conferma + token.UsedAt atomicamente

        if (improvements.Count > 0)
        {
            var circle = await db.Circles.FindAsync(match.CircleId);
            var circleName = circle?.Name ?? "";
            foreach (var (improvedUserId, newPos) in improvements)
                _ = pushService.SendRankingImprovedAsync(improvedUserId, newPos, circleName);
        }

        var user = await db.Users.FindAsync(confirmToken.UserId);
        return Results.Ok(new { status, confirmationsCount = totalCount, isActivated = user?.IsActivated ?? true });
    }

    // ─── POST /m/{token}/dispute ──────────────────────────────────────────────

    private static async Task<IResult> DisputeViaTokenAsync(
        Guid token,
        AppDbContext db,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        var (confirmToken, match, validateResult) = await ValidateTokenAsync(token, db);
        if (validateResult != null) return validateResult;

        if (confirmToken!.UsedAt != null)
            return Results.Ok(new { alreadyDone = true, status = match!.Status });

        if (match!.Status is "confirmed" or "disputed")
            return Results.Conflict(new { error = $"La partita è già {match.Status}" });

        MatchEndpoints.PrepareDispute(match);
        confirmToken.UsedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); // salva status + token.UsedAt atomicamente

        var circleId = match.CircleId;
        var matchId = match.Id;
        var frontendBase = configuration["Cors:AllowedOrigins:0"] ?? "http://localhost:4200";
        var matchLink = $"{frontendBase}/circles/{circleId}/matches/{matchId}";
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var owner = await scopedDb.Circles
                    .Where(c => c.Id == circleId)
                    .Select(c => new { c.Name, c.Owner.Email })
                    .FirstOrDefaultAsync();
                if (owner?.Email != null)
                    await emailService.SendMatchDisputedEmailAsync(owner.Email, owner.Name, matchLink);
            }
            catch (Exception ex)
            {
                loggerFactory.CreateLogger(nameof(PublicMatchEndpoints))
                    .LogWarning(ex, "Dispute email dispatch failed for match {MatchId}", matchId);
            }
        });

        var user = await db.Users.FindAsync(confirmToken.UserId);
        return Results.Ok(new { status = match.Status, isActivated = user?.IsActivated ?? true });
    }

    // ─── Token validation helper ──────────────────────────────────────────────

    private static async Task<(Data.Entities.MatchConfirmationToken? Token, Data.Entities.Match? Match, IResult? Error)>
        ValidateTokenAsync(Guid token, AppDbContext db)
    {
        var confirmToken = await db.MatchConfirmationTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (confirmToken == null)
            return (null, null, Results.NotFound(new { error = "Token non trovato" }));

        if (confirmToken.ExpiresAt < DateTimeOffset.UtcNow)
            return (null, null, Results.Json(new { error = "Token scaduto" }, statusCode: 410));

        var match = await db.Matches.FindAsync(confirmToken.MatchId);
        if (match == null)
            return (null, null, Results.NotFound(new { error = "Partita non trovata" }));

        return (confirmToken, match, null);
    }
}
