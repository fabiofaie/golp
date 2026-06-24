using System.ComponentModel.DataAnnotations;
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
        circles.MapPost("/{id:guid}/members", AddMemberAsync);

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
        ISportsService sportsService)
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
            })
            .ToListAsync();

        return Results.Ok(memberships.Select(m => new
        {
            id          = m.CircleId,
            name        = m.CircleName,
            sport       = m.CircleSport,
            sets        = m.CircleSets,
            pointUnit   = m.CirclePointUnit,
            ownerId     = m.CircleOwnerId,
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

        var circleExists = await db.Circles.AnyAsync(c => c.Id == id);
        if (!circleExists)
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
            .Select(m => new { m.UserId, Name = m.User.Name, m.Rating })
            .ToListAsync();

        var classified = members
            .Where(m => confirmedCounts.ContainsKey(m.UserId))
            .OrderByDescending(m => m.Rating)
            .ThenByDescending(m => confirmedCounts[m.UserId])
            .Select((m, i) => new
            {
                userId           = m.UserId,
                name             = m.Name,
                rating           = m.Rating,
                rank             = i + 1,
                confirmedMatches = confirmedCounts[m.UserId],
            })
            .ToList();

        var unclassified = members
            .Where(m => !confirmedCounts.ContainsKey(m.UserId))
            .Select(m => new { userId = m.UserId, name = m.Name })
            .ToList();

        return Results.Ok(new { classified, unclassified });
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

    // POST /circles/{id}/members — solo owner. Aggiunge un giocatore al circolo:
    // - email esistente + confirmed=false → ritorna il nome per conferma, nessun side-effect
    // - email esistente + confirmed=true  → crea la membership (idempotente)
    // - email non esistente + name        → crea utente "pending" (PasswordHash vuoto) + membership + invio email attivazione
    private static async Task<IResult> AddMemberAsync(
        Guid id,
        AddMemberRequest req,
        ClaimsPrincipal user,
        AppDbContext db,
        IPasswordResetService resetService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circle = await db.Circles.FindAsync(id);
        if (circle == null)
            return Results.NotFound(new { error = "Circolo non trovato" });

        if (circle.OwnerId != userId)
            return Results.Json(new { error = "Solo il creatore può aggiungere giocatori" }, statusCode: 403);

        if (!IsValidEmail(req.Email))
            return Results.BadRequest(new { error = "Formato email non valido" });

        var email = req.Email.Trim().ToLowerInvariant();
        var existingUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Email esistente, non ancora confermata dall'owner → ritorna solo il nome, nessun side-effect
        if (existingUser != null && !req.Confirmed)
            return Results.Ok(new { exists = true, name = existingUser.Name });

        // Email esistente e confermata → crea membership (idempotente)
        if (existingUser != null && req.Confirmed)
        {
            var alreadyMember = await db.CircleMemberships
                .AnyAsync(m => m.CircleId == id && m.UserId == existingUser.Id);

            if (alreadyMember)
                return Results.Ok(new { exists = true, alreadyMember = true, name = existingUser.Name });

            db.CircleMemberships.Add(new CircleMembership
            {
                CircleId = id,
                UserId   = existingUser.Id,
                Rating   = 1000,
            });
            await db.SaveChangesAsync();

            await emailService.SendAddedToCircleNotificationAsync(existingUser.Email, circle.Name);

            return Results.Ok(new { exists = true, alreadyMember = false, name = existingUser.Name });
        }

        // Email non esistente, nome non ancora fornito → ritorna solo l'esito del lookup, nessun side-effect
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.Ok(new { exists = false });

        // Email non esistente + nome fornito → crea nuovo utente pending + membership + email di attivazione

        var newUser = new User
        {
            Name         = req.Name.Trim(),
            Email        = email,
            PasswordHash = string.Empty,
        };

        db.Users.Add(newUser);
        db.CircleMemberships.Add(new CircleMembership
        {
            CircleId = id,
            UserId   = newUser.Id,
            Rating   = 1000,
        });
        await db.SaveChangesAsync();

        var plainToken = await resetService.GenerateTokenAsync(newUser.Id);
        var frontendBase = configuration["Cors:AllowedOrigins:0"] ?? "http://localhost:4200";
        var activationLink = $"{frontendBase}/reset-password?token={Uri.EscapeDataString(plainToken)}";
        await emailService.SendCircleActivationEmailAsync(newUser.Email, circle.Name, activationLink);

        return Results.Ok(new { exists = false, created = true, name = newUser.Name });
    }

    private static bool IsValidEmail(string email) =>
        new EmailAddressAttribute().IsValid(email);
}

record CreateCircleRequest(string Name, string Sport);
record JoinByTokenRequest(string InviteToken);
record AddMemberRequest(string Email, string? Name, bool Confirmed);
