using System.Security.Claims;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class MatchEndpoints
{
    public static IEndpointRouteBuilder MapMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var matches = app.MapGroup("/circles/{circleId:guid}/matches").RequireAuthorization();
        matches.MapPost("/", CreateMatchAsync);
        matches.MapGet("/", GetMatchesAsync);
        matches.MapGet("/{matchId:guid}", GetMatchDetailAsync);
        matches.MapPost("/{matchId:guid}/confirm", ConfirmMatchAsync);
        matches.MapPost("/{matchId:guid}/dispute", DisputeMatchAsync);
        matches.MapPost("/{matchId:guid}/force-confirm", ForceConfirmMatchAsync);
        return app;
    }

    // ─── POST / ───────────────────────────────────────────────────────────────

    private static async Task<IResult> CreateMatchAsync(
        Guid circleId,
        CreateMatchRequest req,
        ClaimsPrincipal user,
        AppDbContext db,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circle = await db.Circles.FindAsync(circleId);
        if (circle == null)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var existingMemberIds = await db.CircleMemberships
            .Where(m => m.CircleId == circleId)
            .Select(m => m.UserId)
            .ToHashSetAsync();

        if (!existingMemberIds.Contains(userId))
            return Results.Json(new { error = "Non sei membro del circolo" }, statusCode: 403);

        if (req.Team1 == null || req.Team1.Length != 2)
            return Results.BadRequest(new { error = "Team1 deve avere esattamente 2 giocatori" });

        if (req.Team2 == null || req.Team2.Length != 2)
            return Results.BadRequest(new { error = "Team2 deve avere esattamente 2 giocatori" });

        // Validate each slot structure
        foreach (var slot in req.Team1.Concat(req.Team2))
        {
            if (slot.UserId == null && (string.IsNullOrWhiteSpace(slot.GuestName) ||
                (string.IsNullOrWhiteSpace(slot.GuestEmail) && string.IsNullOrWhiteSpace(slot.GuestPhone))))
                return Results.BadRequest(new { error = "Slot ospite richiede nome e almeno email o telefono" });
        }

        // Resolve all 4 player IDs (find-or-create for guest slots)
        var resolvedIds = new Guid[4];
        var allSlots = req.Team1.Concat(req.Team2).ToArray();
        for (int i = 0; i < 4; i++)
        {
            var slot = allSlots[i];
            if (slot.UserId.HasValue)
            {
                if (!existingMemberIds.Contains(slot.UserId.Value))
                    return Results.BadRequest(new { error = $"Il giocatore {slot.UserId.Value} non è membro del circolo" });
                resolvedIds[i] = slot.UserId.Value;
            }
            else
            {
                resolvedIds[i] = await ResolveOrCreateGuestAsync(slot, circleId, db);
            }
        }

        if (resolvedIds.Distinct().Count() != 4)
            return Results.BadRequest(new { error = "I 4 giocatori devono essere tutti distinti" });

        var team1Ids = resolvedIds[0..2];
        var team2Ids = resolvedIds[2..4];

        var creatorInTeam = team1Ids.Contains(userId) || team2Ids.Contains(userId);
        if (!creatorInTeam && circle.OwnerId != userId)
            return Results.BadRequest(new { error = "L'inseritore deve essere uno dei 4 giocatori o il proprietario del circolo" });

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
            {
                int totalGamesTeam1 = req.Sets.Sum(s => s.Team1);
                int totalGamesTeam2 = req.Sets.Sum(s => s.Team2);
                if (totalGamesTeam1 == totalGamesTeam2)
                    return Results.BadRequest(new { error = "La partita deve avere un vincitore (pareggio totale non ammesso)" });
                winnerTeam = totalGamesTeam1 > totalGamesTeam2 ? 1 : 2;
            }
            else
            {
                winnerTeam = team1Wins > team2Wins ? 1 : 2;
            }
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
            Team1Player1Id = team1Ids[0],
            Team1Player2Id = team1Ids[1],
            Team2Player1Id = team2Ids[0],
            Team2Player2Id = team2Ids[1],
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

        // Inseritore = conferma implicita 1/4, solo se è uno dei 4 giocatori
        // (l'owner che registra per altri non partecipa: non può "confermare" un risultato suo).
        if (creatorInTeam)
        {
            db.MatchConfirmations.Add(new MatchConfirmation
            {
                MatchId     = match.Id,
                UserId      = userId,
                ConfirmedAt = DateTimeOffset.UtcNow,
            });
        }

        // US-040: token pubblici per ogni giocatore non-creatore (salvati atomicamente con la partita).
        var recipientIds = resolvedIds.Where(id => id != userId).ToArray();
        var confirmationTokens = recipientIds.Select(rid => new MatchConfirmationToken
        {
            MatchId   = match.Id,
            UserId    = rid,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(72),
        }).ToList();
        db.MatchConfirmationTokens.AddRange(confirmationTokens);

        await db.SaveChangesAsync();

        // US-006/US-020: push + email ai 3 da confermare (escluso l'inseritore), fire-and-forget.
        // Scope DI nuovo: quello della request viene disposed alla risposta.
        var matchId = match.Id;
        var circleName = circle.Name;
        var frontendBase = configuration["Cors:AllowedOrigins:0"] ?? "http://localhost:4200";
        // US-040: mappa userId→tokenLink per inviare link individuali.
        var tokenLinkByUser = confirmationTokens.ToDictionary(t => t.UserId, t => $"{frontendBase}/m/{t.Token}");

        // US-042: dati dei destinatari per il componente share (nome + phone per wa.me / Web Share).
        var recipientData = await db.Users
            .Where(u => recipientIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.Phone })
            .ToListAsync();
        var confirmationLinks = confirmationTokens.Select(t =>
        {
            var u = recipientData.First(x => x.Id == t.UserId);
            return new { userId = u.Id, name = u.Name, phone = u.Phone, tokenUrl = tokenLinkByUser[t.UserId] };
        }).ToList();

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var pushService = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                await pushService.SendConfirmationRequestAsync(matchId, circleId, recipientIds);
            }
            catch (Exception ex)
            {
                // fire-and-forget: nessuna eccezione deve emergere come unobserved task,
                // ma un errore di risoluzione DI non deve restare invisibile
                loggerFactory.CreateLogger(nameof(MatchEndpoints))
                    .LogWarning(ex, "Push dispatch failed for match {MatchId}", matchId);
            }

            var emailLogger = loggerFactory.CreateLogger(nameof(MatchEndpoints));
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var recipientUsers = await scopedDb.Users
                    .Where(u => recipientIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.Email })
                    .ToListAsync();

                foreach (var r in recipientUsers.Where(r => r.Email == null))
                    emailLogger.LogInformation("Skipping email for phone-only guest {UserId}, match {MatchId}", r.Id, matchId);

                foreach (var r in recipientUsers.Where(r => r.Email != null))
                {
                    var tokenLink = tokenLinkByUser.GetValueOrDefault(r.Id, $"{frontendBase}/circles/{circleId}/matches/{matchId}");
                    try
                    {
                        await emailService.SendMatchConfirmationRequestEmailAsync(r.Email!, circleName, tokenLink);
                    }
                    catch (Exception ex)
                    {
                        // un destinatario che fallisce non deve impedire l'invio agli altri
                        emailLogger.LogWarning(ex, "Confirmation email failed for {Email}, match {MatchId}", r.Email, matchId);
                    }
                }
            }
            catch (Exception ex)
            {
                emailLogger.LogWarning(ex, "Confirmation email dispatch failed for match {MatchId}", matchId);
            }
        });

        return Results.Created($"/circles/{circleId}/matches/{match.Id}", new
        {
            id                = match.Id,
            circleId          = match.CircleId,
            status            = match.Status,
            winnerTeam        = match.WinnerTeam,
            createdAt         = match.CreatedAt,
            confirmationLinks,
        });
    }

    // ─── GET / ────────────────────────────────────────────────────────────────

    private static async Task<IResult> GetMatchesAsync(
        Guid circleId,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circleExists = await db.Circles.AnyAsync(c => c.Id == circleId);
        if (!circleExists)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var matches = await db.Matches
            .Where(m => m.CircleId == circleId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

        if (matches.Count == 0)
            return Results.Ok(Array.Empty<object>());

        // Collect all player IDs to resolve names in one query
        var playerIds = matches
            .SelectMany(m => new[] { m.Team1Player1Id, m.Team1Player2Id, m.Team2Player1Id, m.Team2Player2Id })
            .Distinct()
            .ToHashSet();

        var userInfos = await db.Users
            .Where(u => playerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.IsActivated })
            .ToDictionaryAsync(u => u.Id);

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

        var result = matches.Select(m =>
        {
            int? myDelta = null;
            if (m.Status == "confirmed")
            {
                if      (m.Team1Player1Id == userId) myDelta = m.DeltaTeam1Player1;
                else if (m.Team1Player2Id == userId) myDelta = m.DeltaTeam1Player2;
                else if (m.Team2Player1Id == userId) myDelta = m.DeltaTeam2Player1;
                else if (m.Team2Player2Id == userId) myDelta = m.DeltaTeam2Player2;
            }
            return new
            {
                id                       = m.Id,
                status                   = m.Status,
                winnerTeam               = m.WinnerTeam,
                createdAt                = m.CreatedAt,
                myDelta,
                confirmationsCount       = confirmationCounts.GetValueOrDefault(m.Id, 0),
                hasCurrentUserConfirmed = userConfirmedSet.Contains(m.Id),
                team1 = new[]
                {
                    new { userId = m.Team1Player1Id, name = userInfos.GetValueOrDefault(m.Team1Player1Id)?.Name ?? "", isActivated = userInfos.GetValueOrDefault(m.Team1Player1Id)?.IsActivated ?? true },
                    new { userId = m.Team1Player2Id, name = userInfos.GetValueOrDefault(m.Team1Player2Id)?.Name ?? "", isActivated = userInfos.GetValueOrDefault(m.Team1Player2Id)?.IsActivated ?? true },
                },
                team2 = new[]
                {
                    new { userId = m.Team2Player1Id, name = userInfos.GetValueOrDefault(m.Team2Player1Id)?.Name ?? "", isActivated = userInfos.GetValueOrDefault(m.Team2Player1Id)?.IsActivated ?? true },
                    new { userId = m.Team2Player2Id, name = userInfos.GetValueOrDefault(m.Team2Player2Id)?.Name ?? "", isActivated = userInfos.GetValueOrDefault(m.Team2Player2Id)?.IsActivated ?? true },
                },
            };
        });

        return Results.Ok(result);
    }

    // ─── GET /{matchId} ──────────────────────────────────────────────────────

    private static async Task<IResult> GetMatchDetailAsync(
        Guid circleId,
        Guid matchId,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var match = await db.Matches.FindAsync(matchId);
        if (match == null || match.CircleId != circleId)
            return Results.NotFound(new { error = "Partita non trovata" });

        var isMember = await db.CircleMemberships.AnyAsync(m => m.CircleId == circleId && m.UserId == userId);
        if (!isMember)
            return Results.Json(new { error = "Non sei membro del circolo" }, statusCode: 403);

        var playerIds = new[] { match.Team1Player1Id, match.Team1Player2Id, match.Team2Player1Id, match.Team2Player2Id };
        var playerInfos = await db.Users
            .Where(u => playerIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.IsActivated })
            .ToDictionaryAsync(u => u.Id);
        // keep userNames for compat with existing lookups below
        var userNames = playerInfos.ToDictionary(kv => kv.Key, kv => kv.Value.Name);

        var sets = await db.MatchSets
            .Where(s => s.MatchId == matchId)
            .OrderBy(s => s.SetNumber)
            .Select(s => new { s.Team1Score, s.Team2Score })
            .ToListAsync();

        var confirmationsCount = await db.MatchConfirmations.CountAsync(c => c.MatchId == matchId);
        var hasCurrentUserConfirmed = await db.MatchConfirmations.AnyAsync(c => c.MatchId == matchId && c.UserId == userId);
        var confirmedUserIds = await db.MatchConfirmations
            .Where(c => c.MatchId == matchId)
            .Select(c => c.UserId)
            .ToListAsync();

        DateTimeOffset? confirmedAt = null;
        string? confirmedByName = null;
        bool? isForced = null;
        object? deltas = null;

        if (match.Status == "confirmed")
        {
            if (match.ForceConfirmedById is { } forcedById)
            {
                confirmedAt = match.ForceConfirmedAt;
                isForced = true;
                confirmedByName = userNames.TryGetValue(forcedById, out var forcedName)
                    ? forcedName
                    : await db.Users.Where(u => u.Id == forcedById).Select(u => u.Name).FirstOrDefaultAsync();
            }
            else
            {
                var decisive = await db.MatchConfirmations
                    .Where(c => c.MatchId == matchId)
                    .OrderByDescending(c => c.ConfirmedAt)
                    .FirstOrDefaultAsync();
                if (decisive != null)
                {
                    confirmedAt = decisive.ConfirmedAt;
                    confirmedByName = userNames.GetValueOrDefault(decisive.UserId, "");
                    isForced = false;
                }
            }

            deltas = new[]
            {
                new { userId = match.Team1Player1Id, delta = match.DeltaTeam1Player1 },
                new { userId = match.Team1Player2Id, delta = match.DeltaTeam1Player2 },
                new { userId = match.Team2Player1Id, delta = match.DeltaTeam2Player1 },
                new { userId = match.Team2Player2Id, delta = match.DeltaTeam2Player2 },
            };
        }

        return Results.Ok(new
        {
            id                      = match.Id,
            status                  = match.Status,
            winnerTeam              = match.WinnerTeam,
            createdAt               = match.CreatedAt,
            confirmationsCount,
            hasCurrentUserConfirmed,
            confirmations           = confirmedUserIds,
            isParticipant           = playerIds.Contains(userId),
            confirmedAt,
            confirmedByName,
            isForced,
            deltas,
            sets,
            team1 = new[]
            {
                new { userId = match.Team1Player1Id, name = userNames.GetValueOrDefault(match.Team1Player1Id, ""), isActivated = playerInfos.GetValueOrDefault(match.Team1Player1Id)?.IsActivated ?? true },
                new { userId = match.Team1Player2Id, name = userNames.GetValueOrDefault(match.Team1Player2Id, ""), isActivated = playerInfos.GetValueOrDefault(match.Team1Player2Id)?.IsActivated ?? true },
            },
            team2 = new[]
            {
                new { userId = match.Team2Player1Id, name = userNames.GetValueOrDefault(match.Team2Player1Id, ""), isActivated = playerInfos.GetValueOrDefault(match.Team2Player1Id)?.IsActivated ?? true },
                new { userId = match.Team2Player2Id, name = userNames.GetValueOrDefault(match.Team2Player2Id, ""), isActivated = playerInfos.GetValueOrDefault(match.Team2Player2Id)?.IsActivated ?? true },
            },
        });
    }

    // ─── POST /{matchId}/confirm ──────────────────────────────────────────────

    private static async Task<IResult> ConfirmMatchAsync(
        Guid circleId,
        Guid matchId,
        ClaimsPrincipal user,
        AppDbContext db,
        IRatingService ratingService,
        IPushNotificationService pushService)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var match = await db.Matches.FindAsync(matchId);
        if (match == null || match.CircleId != circleId)
            return Results.NotFound(new { error = "Partita non trovata" });

        if (match.Status is "confirmed" or "disputed")
            return Results.Conflict(new { error = $"La partita è già {match.Status}" });

        var playerIds = new[] { match.Team1Player1Id, match.Team1Player2Id, match.Team2Player1Id, match.Team2Player2Id };
        if (!playerIds.Contains(userId))
            return Results.Json(new { error = "Non sei un partecipante di questa partita" }, statusCode: 403);

        var (_, status, totalCount, improvements) = await PrepareConfirmAsync(matchId, userId, match, db, ratingService);
        await db.SaveChangesAsync();

        // US-035: notifiche push fire-and-forget per chi sale in classifica
        if (improvements.Count > 0)
        {
            var circle = await db.Circles.FindAsync(circleId);
            var circleName = circle?.Name ?? "";
            foreach (var (improvedUserId, newPos) in improvements)
                _ = pushService.SendRankingImprovedAsync(improvedUserId, newPos, circleName);
        }

        return Results.Ok(new { status, confirmationsCount = totalCount });
    }

    // ─── POST /{matchId}/dispute ──────────────────────────────────────────────

    private static async Task<IResult> DisputeMatchAsync(
        Guid circleId,
        Guid matchId,
        ClaimsPrincipal user,
        AppDbContext db,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var match = await db.Matches.FindAsync(matchId);
        if (match == null || match.CircleId != circleId)
            return Results.NotFound(new { error = "Partita non trovata" });

        if (match.Status is "confirmed" or "disputed")
            return Results.Conflict(new { error = $"La partita è già {match.Status}" });

        var playerIds = new[] { match.Team1Player1Id, match.Team1Player2Id, match.Team2Player1Id, match.Team2Player2Id };
        if (!playerIds.Contains(userId))
            return Results.Json(new { error = "Non sei un partecipante di questa partita" }, statusCode: 403);

        PrepareDispute(match);
        await db.SaveChangesAsync();

        // US-020: notifica email all'owner del circolo, fire-and-forget (non blocca la dispute).
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
                loggerFactory.CreateLogger(nameof(MatchEndpoints))
                    .LogWarning(ex, "Dispute email dispatch failed for match {MatchId}", matchId);
            }
        });

        return Results.Ok(new { status = match.Status });
    }

    // ─── Shared helpers (usati da endpoint autenticati e pubblici) ───────────

    // PrepareConfirm stages changes in the EF tracker without saving; caller must call SaveChangesAsync.
    internal static async Task<(bool alreadyDone, string status, int count, IReadOnlyList<(Guid UserId, int NewPosition)> improvements)>
        PrepareConfirmAsync(Guid matchId, Guid userId, Match match, AppDbContext db, IRatingService ratingService)
    {
        var alreadyConfirmed = await db.MatchConfirmations
            .AnyAsync(c => c.MatchId == matchId && c.UserId == userId);

        if (!alreadyConfirmed)
            db.MatchConfirmations.Add(new MatchConfirmation { MatchId = matchId, UserId = userId, ConfirmedAt = DateTimeOffset.UtcNow });

        var existingCount = await db.MatchConfirmations.CountAsync(c => c.MatchId == matchId);
        var totalCount = existingCount + (alreadyConfirmed ? 0 : 1);

        IReadOnlyList<(Guid UserId, int NewPosition)> improvements = [];
        if (totalCount == 4 && !alreadyConfirmed)
        {
            match.Status = "confirmed";
            improvements = await ratingService.CalculateAndApplyAsync(matchId, db);
        }

        return (alreadyConfirmed, match.Status, totalCount, improvements);
    }

    // PrepareDispute stages the status change without saving; caller must call SaveChangesAsync.
    internal static void PrepareDispute(Match match)
    {
        match.Status = "disputed";
    }

    // ─── POST /{matchId}/force-confirm ───────────────────────────────────────────

    private static async Task<IResult> ForceConfirmMatchAsync(
        Guid circleId,
        Guid matchId,
        ClaimsPrincipal user,
        AppDbContext db,
        IRatingService ratingService)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var circle = await db.Circles.FindAsync(circleId);
        if (circle == null)
            return Results.NotFound(new { error = "Circolo non trovato" });

        if (circle.OwnerId != userId)
            return Results.Json(new { error = "Solo il proprietario del circolo può eseguire questa azione" }, statusCode: 403);

        var match = await db.Matches.FindAsync(matchId);
        if (match == null || match.CircleId != circleId)
            return Results.NotFound(new { error = "Partita non trovata" });

        if (match.Status != "pending")
            return Results.BadRequest(new { error = "La partita non è in stato pending" });

        match.Status = "confirmed";
        match.ForceConfirmedById = userId;
        match.ForceConfirmedAt = DateTimeOffset.UtcNow;

        _ = await ratingService.CalculateAndApplyAsync(matchId, db);
        await db.SaveChangesAsync();

        return Results.Ok(new { status = match.Status, forceConfirmedBy = userId });
    }

    private static async Task<Guid> ResolveOrCreateGuestAsync(
        PlayerSlotDto slot,
        Guid circleId,
        AppDbContext db)
    {
        User? existing = null;

        if (!string.IsNullOrWhiteSpace(slot.GuestEmail))
        {
            var email = slot.GuestEmail.Trim().ToLowerInvariant();
            existing = db.Users.Local.FirstOrDefault(u => u.Email == email)
                       ?? await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        if (existing == null && !string.IsNullOrWhiteSpace(slot.GuestPhone))
        {
            var phone = slot.GuestPhone.Trim();
            existing = db.Users.Local.FirstOrDefault(u => u.Phone == phone)
                       ?? await db.Users.FirstOrDefaultAsync(u => u.Phone == phone);
        }

        if (existing == null)
        {
            existing = new User
            {
                Name         = slot.GuestName!.Trim(),
                Email        = string.IsNullOrWhiteSpace(slot.GuestEmail) ? null : slot.GuestEmail.Trim().ToLowerInvariant(),
                Phone        = string.IsNullOrWhiteSpace(slot.GuestPhone) ? null : slot.GuestPhone.Trim(),
                PasswordHash = string.Empty,
                IsActivated  = false,
            };
            db.Users.Add(existing);
        }

        var alreadyMember = await db.CircleMemberships
            .AnyAsync(m => m.CircleId == circleId && m.UserId == existing.Id);

        if (!alreadyMember)
            db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = existing.Id, Rating = 1000 });

        return existing.Id;
    }
}

record PlayerSlotDto(Guid? UserId, string? GuestName, string? GuestEmail, string? GuestPhone);
record CreateMatchRequest(PlayerSlotDto[] Team1, PlayerSlotDto[] Team2, SetScoreDto[] Sets);
record SetScoreDto(int Team1, int Team2);
