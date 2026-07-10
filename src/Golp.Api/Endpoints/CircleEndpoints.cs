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
        app.MapGet("/sports", GetSportsAsync);
        app.MapGet("/circles/invite/{token}", GetInviteInfoAsync);

        var circles = app.MapGroup("/circles").RequireAuthorization();
        circles.MapGet("/", GetAllCirclesAsync);
        circles.MapPost("/", CreateCircleAsync);
        circles.MapGet("/me", GetMyCirclesAsync);
        circles.MapPost("/join-by-token", JoinByTokenAsync);
        circles.MapPost("/{id:guid}/join", JoinCircleAsync);
        circles.MapGet("/{id:guid}/members", GetCircleMembersAsync);
        circles.MapGet("/{id:guid}/leaderboard", GetCircleLeaderboardAsync);
        circles.MapGet("/{id:guid}/invite-link", GetInviteLinkAsync);
        circles.MapPut("/{id:guid}/rating-config", UpdateRatingConfigAsync);

        return app;
    }

    // GET /sports — public
    private static async Task<IResult> GetSportsAsync(ISportsService sportsService) =>
        Results.Ok(await sportsService.GetAllAsync());

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
        AppDbContext db,
        ISportsService sportsService,
        IEmailService emailService,
        ILoggerFactory loggerFactory)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { error = "Il nome è obbligatorio" });

        if (req.Name.Length > 100)
            return Results.BadRequest(new { error = "Il nome non può superare i 100 caratteri" });

        var sportConfig = await sportsService.GetBySportAsync(req.Sport);
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

        var logger = loggerFactory.CreateLogger(nameof(CircleEndpoints));
        var ownerEmail = user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
        _ = emailService.SendNewCircleNotificationAsync(circle.Name, circle.Sport, ownerEmail, DateTime.UtcNow)
            .ContinueWith(t => logger.LogError(t.Exception, "Staff circle notification failed"),
                          TaskContinuationOptions.OnlyOnFaulted);

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
                CircleName       = m.Circle.Name,
                CircleSport      = m.Circle.Sport,
                CircleSets       = m.Circle.Sets,
                CirclePointUnit  = m.Circle.PointUnit,
                CircleOwnerId    = m.Circle.OwnerId,
                MyRating         = m.Rating,
                MemberCount      = db.CircleMemberships.Count(x => x.CircleId == m.CircleId),
                MyRank           = db.CircleMemberships.Count(x => x.CircleId == m.CircleId && x.Rating > m.Rating) + 1,
                JoinedAt                = m.JoinedAt,
                RatingMethod            = m.Circle.RatingMethod,
                GameBonusWindowMatches  = m.Circle.GameBonusWindowMatches,
                GameBonusWindowWeeks    = m.Circle.GameBonusWindowWeeks,
            })
            .ToListAsync();

        return Results.Ok(memberships.Select(m => new
        {
            id                     = m.CircleId,
            name                   = m.CircleName,
            sport                  = m.CircleSport,
            sets                   = m.CircleSets,
            pointUnit              = m.CirclePointUnit,
            ownerId                = m.CircleOwnerId,
            memberCount            = m.MemberCount,
            myRating               = m.MyRating,
            myRank                 = m.MyRank,
            joinedAt               = m.JoinedAt,
            ratingMethod           = m.RatingMethod,
            gameBonusWindowMatches = m.GameBonusWindowMatches,
            gameBonusWindowWeeks   = m.GameBonusWindowWeeks,
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
                Name        = m.User.Name,
                m.Rating,
                IsActivated = m.User.IsActivated,
            })
            .ToListAsync();

        return Results.Ok(members.Select((m, i) => new
        {
            userId      = m.UserId,
            name        = m.Name,
            rating      = m.Rating,
            rank        = i + 1,
            isActivated = m.IsActivated,
        }));
    }

    // GET /circles/{id}/leaderboard
    // classified = members with ≥1 confirmed match, ordered by rating DESC, confirmedMatches DESC
    // unclassified = members with 0 confirmed matches
    private static async Task<IResult> GetCircleLeaderboardAsync(
        Guid id,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out _))
            return Results.Unauthorized();

        var circle = await db.Circles.FirstOrDefaultAsync(c => c.Id == id);
        if (circle == null)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var confirmedMatches = await db.Matches
            .Where(m => m.CircleId == id && m.Status == "confirmed")
            .Select(m => new { m.Team1Player1Id, m.Team1Player2Id, m.Team2Player1Id, m.Team2Player2Id })
            .ToListAsync();

        var confirmedCounts = confirmedMatches
            .SelectMany(m => new[] { m.Team1Player1Id, m.Team1Player2Id, m.Team2Player1Id, m.Team2Player2Id })
            .GroupBy(uid => uid)
            .ToDictionary(g => g.Key, g => g.Count());

        var members = await db.CircleMemberships
            .Where(m => m.CircleId == id)
            .Select(m => new { m.UserId, Name = m.User.Name, m.Rating, IsActivated = m.User.IsActivated })
            .ToListAsync();

        // US-052: circoli con metodo Game+Bonus attivo ordinano/mostrano i punti in finestra (N∩M) invece del rating ELO
        Dictionary<Guid, int>? gameBonusScores = null;
        if (circle.RatingMethod == "GameBonus")
            gameBonusScores = await GameBonusRatingService.GetWindowScoresAsync(
                db, id, circle.GameBonusWindowMatches, circle.GameBonusWindowWeeks, members.Select(m => m.UserId));

        var classified = members
            .Where(m => confirmedCounts.ContainsKey(m.UserId))
            .OrderByDescending(m => gameBonusScores != null ? gameBonusScores.GetValueOrDefault(m.UserId, 0) : m.Rating)
            .ThenByDescending(m => confirmedCounts[m.UserId])
            .Select((m, i) => new
            {
                userId           = m.UserId,
                name             = m.Name,
                rating           = m.Rating,
                gameBonusPoints  = gameBonusScores?.GetValueOrDefault(m.UserId, 0),
                rank             = i + 1,
                confirmedMatches = confirmedCounts[m.UserId],
                isActivated      = m.IsActivated,
            })
            .ToList();

        var unclassified = members
            .Where(m => !confirmedCounts.ContainsKey(m.UserId))
            .Select(m => new { userId = m.UserId, name = m.Name, isActivated = m.IsActivated })
            .ToList();

        return Results.Ok(new { classified, unclassified, ratingMethod = circle.RatingMethod });
    }

    // GET /circles/invite/{token} — public, valida un token senza consumarlo
    private static async Task<IResult> GetInviteInfoAsync(
        string token,
        AppDbContext db)
    {
        var circle = await db.Circles.FirstOrDefaultAsync(c => c.JoinCode == token);
        if (circle == null)
            return Results.NotFound(new { valid = false });

        return Results.Ok(new { valid = true, circleName = circle.Name });
    }

    // POST /circles/join-by-token — join tramite inviteToken (JoinCode)
    private static async Task<IResult> JoinByTokenAsync(
        JoinByTokenRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circle = await db.Circles.FirstOrDefaultAsync(c => c.JoinCode == req.InviteToken);
        if (circle == null)
            return Results.NotFound(new { error = "Link non valido o scaduto" });

        var membership = await db.CircleMemberships
            .FirstOrDefaultAsync(m => m.CircleId == circle.Id && m.UserId == userId);

        if (membership != null)
            return Results.Ok(new { circleId = circle.Id, myRating = membership.Rating, alreadyMember = true });

        db.CircleMemberships.Add(new CircleMembership
        {
            CircleId = circle.Id,
            UserId   = userId,
            Rating   = 1000,
        });
        await db.SaveChangesAsync();

        return Results.Ok(new { circleId = circle.Id, myRating = 1000, alreadyMember = false });
    }

    // GET /circles/{id}/invite-link — solo owner, genera token lazy
    private static async Task<IResult> GetInviteLinkAsync(
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

        if (circle.OwnerId != userId)
            return Results.Json(new { error = "Solo il creatore può generare il link di invito" }, statusCode: 403);

        if (circle.JoinCode == null)
        {
            circle.JoinCode = Guid.NewGuid().ToString("N");
            await db.SaveChangesAsync();
        }

        return Results.Ok(new { inviteToken = circle.JoinCode });
    }

    // PUT /circles/{id}/rating-config — solo owner. Cambia il metodo di calcolo punteggio del circolo
    // (US-051/US-052) e i parametri di finestra del metodo Game+Bonus. Non ricalcola lo storico.
    private static async Task<IResult> UpdateRatingConfigAsync(
        Guid id,
        UpdateRatingConfigRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circle = await db.Circles.FindAsync(id);
        if (circle == null)
            return Results.NotFound(new { error = "Circolo non trovato" });

        if (circle.OwnerId != userId)
            return Results.Json(new { error = "Solo il proprietario del circolo può cambiare il metodo di calcolo" }, statusCode: 403);

        if (req.RatingMethod is not ("Elo" or "GameBonus"))
            return Results.BadRequest(new { error = "ratingMethod deve essere 'Elo' o 'GameBonus'" });

        var windowMatches = req.GameBonusWindowMatches ?? circle.GameBonusWindowMatches;
        var windowWeeks   = req.GameBonusWindowWeeks ?? circle.GameBonusWindowWeeks;

        if (windowMatches is < 1 or > 200)
            return Results.BadRequest(new { error = "gameBonusWindowMatches deve essere tra 1 e 200" });
        if (windowWeeks is < 1 or > 52)
            return Results.BadRequest(new { error = "gameBonusWindowWeeks deve essere tra 1 e 52" });

        circle.RatingMethod           = req.RatingMethod;
        circle.GameBonusWindowMatches = windowMatches;
        circle.GameBonusWindowWeeks   = windowWeeks;
        await db.SaveChangesAsync();

        return Results.Ok(new
        {
            ratingMethod           = circle.RatingMethod,
            gameBonusWindowMatches = circle.GameBonusWindowMatches,
            gameBonusWindowWeeks   = circle.GameBonusWindowWeeks,
        });
    }
}

record CreateCircleRequest(string Name, string Sport);
record JoinByTokenRequest(string InviteToken);
record UpdateRatingConfigRequest(string RatingMethod, int? GameBonusWindowMatches, int? GameBonusWindowWeeks);
