using System.Security.Claims;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Golp.Api.Endpoints;

public static class QuickMatchEndpoints
{
    public static IEndpointRouteBuilder MapQuickMatchEndpoints(this IEndpointRouteBuilder app)
    {
        var quick = app.MapGroup("/match/quick").RequireAuthorization();
        quick.MapGet("/suggestions", GetSuggestionsAsync);
        quick.MapPost("/check", CheckCirclesAsync);
        quick.MapPost("/", CreateQuickMatchAsync);
        return app;
    }

    // ─── GET /match/quick/suggestions ─────────────────────────────────────────

    private static async Task<IResult> GetSuggestionsAsync(
        string? q,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        // Source A: players from recent matches
        var myMatches = await db.Matches
            .Where(m => m.Team1Player1Id == userId || m.Team1Player2Id == userId ||
                        m.Team2Player1Id == userId || m.Team2Player2Id == userId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(30)
            .Select(m => new { m.Team1Player1Id, m.Team1Player2Id, m.Team2Player1Id, m.Team2Player2Id, m.CreatedAt })
            .ToListAsync();

        var fromMatches = myMatches.SelectMany(m => new[]
        {
            (UserId: m.Team1Player1Id, LastAt: m.CreatedAt),
            (UserId: m.Team1Player2Id, LastAt: m.CreatedAt),
            (UserId: m.Team2Player1Id, LastAt: m.CreatedAt),
            (UserId: m.Team2Player2Id, LastAt: m.CreatedAt),
        }).Where(x => x.UserId != userId);

        // Source B: other members of circles where user is member
        var myCircleIds = await db.CircleMemberships
            .Where(m => m.UserId == userId)
            .Select(m => m.CircleId)
            .ToListAsync();

        var fromCirclesRaw = myCircleIds.Count == 0
            ? []
            : await db.CircleMemberships
                .Where(m => myCircleIds.Contains(m.CircleId) && m.UserId != userId)
                .Select(m => new { m.UserId, m.JoinedAt })
                .ToListAsync();

        var fromCircles = fromCirclesRaw
            .Select(x => (x.UserId, LastAt: new DateTimeOffset(x.JoinedAt, TimeSpan.Zero)));

        // Union, dedup by userId (keep most recent contact), order by recency
        var combined = fromMatches
            .Concat(fromCircles)
            .GroupBy(x => x.UserId)
            .Select(g => (UserId: g.Key, LastAt: g.Max(x => x.LastAt)))
            .OrderByDescending(x => x.LastAt)
            .ToList();

        if (combined.Count == 0)
            return Results.Ok(Array.Empty<object>());

        var candidateIds = combined.Select(x => x.UserId).ToList();
        var users = await db.Users
            .Where(u => candidateIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Name, u.IsActivated })
            .ToDictionaryAsync(u => u.Id);

        var result = combined
            .Where(x => users.ContainsKey(x.UserId))
            .Where(x => string.IsNullOrWhiteSpace(q) ||
                        users[x.UserId].Name.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(x => new
            {
                userId      = x.UserId,
                name        = users[x.UserId].Name,
                isActivated = users[x.UserId].IsActivated,
            })
            .ToList();

        return Results.Ok(result);
    }

    // ─── POST /match/quick/check ───────────────────────────────────────────────

    private static async Task<IResult> CheckCirclesAsync(
        QuickCheckRequest req,
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var _))
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(req.Sport))
            return Results.BadRequest(new { error = "Sport richiesto" });

        // Resolve guests by email/phone (read-only, no DB writes)
        var knownIds = new List<Guid>(req.UserIds ?? []);
        int unresolvedCount = 0;

        foreach (var guest in req.Guests ?? [])
        {
            User? resolved = null;

            if (!string.IsNullOrWhiteSpace(guest.Email))
            {
                var email = guest.Email.Trim().ToLowerInvariant();
                resolved = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            }

            if (resolved == null && !string.IsNullOrWhiteSpace(guest.Phone))
            {
                var phone = guest.Phone.Trim();
                resolved = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone);
            }

            if (resolved != null)
                knownIds.Add(resolved.Id);
            else
                unresolvedCount++;
        }

        bool isExact = unresolvedCount == 0 && knownIds.Count == 4;
        string mode = isExact ? "exact" : "partial";

        var circlesRaw = new List<(Guid Id, string Name, DateTimeOffset? LastMatchAt)>();

        if (isExact)
        {
            var allIds = knownIds.ToList();
            circlesRaw = await db.Circles
                .Where(c => c.Sport == req.Sport
                         && db.CircleMemberships.Count(m => m.CircleId == c.Id && allIds.Contains(m.UserId)) == 4)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    LastMatchAt = (DateTimeOffset?)db.Matches
                        .Where(m => m.CircleId == c.Id)
                        .Max(m => (DateTimeOffset?)m.CreatedAt),
                })
                .ToListAsync()
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.Name, x.LastMatchAt)).ToList());
        }
        else if (knownIds.Count > 0)
        {
            var knownIdList = knownIds.ToList();
            int requiredCount = knownIdList.Count;
            circlesRaw = await db.Circles
                .Where(c => c.Sport == req.Sport
                         && db.CircleMemberships.Count(m => m.CircleId == c.Id && knownIdList.Contains(m.UserId)) == requiredCount)
                .Select(c => new
                {
                    c.Id,
                    c.Name,
                    LastMatchAt = (DateTimeOffset?)db.Matches
                        .Where(m => m.CircleId == c.Id)
                        .Max(m => (DateTimeOffset?)m.CreatedAt),
                })
                .ToListAsync()
                .ContinueWith(t => t.Result.Select(x => (x.Id, x.Name, x.LastMatchAt)).ToList());
        }

        return Results.Ok(new
        {
            mode,
            circles = circlesRaw.Select(c => new { id = c.Id, name = c.Name, lastMatchAt = c.LastMatchAt }),
        });
    }

    // ─── POST /match/quick ─────────────────────────────────────────────────────

    private static async Task<IResult> CreateQuickMatchAsync(
        QuickMatchRequest req,
        ClaimsPrincipal user,
        AppDbContext db,
        ISportsService sportsService,
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdStr == null || !Guid.TryParse(userIdStr, out var userId))
            return Results.Unauthorized();

        var sport = await sportsService.GetBySportAsync(req.Sport ?? "");
        if (sport == null)
            return Results.BadRequest(new { error = "Sport non valido" });

        if (req.Team1 == null || req.Team1.Length != 2)
            return Results.BadRequest(new { error = "Team1 deve avere esattamente 2 giocatori" });
        if (req.Team2 == null || req.Team2.Length != 2)
            return Results.BadRequest(new { error = "Team2 deve avere esattamente 2 giocatori" });
        if (req.Sets == null || req.Sets.Length == 0)
            return Results.BadRequest(new { error = "Almeno un set richiesto" });

        foreach (var slot in req.Team1.Concat(req.Team2))
        {
            if (slot.UserId == null && (string.IsNullOrWhiteSpace(slot.GuestName) ||
                (string.IsNullOrWhiteSpace(slot.GuestEmail) && string.IsNullOrWhiteSpace(slot.GuestPhone))))
                return Results.BadRequest(new { error = "Slot ospite richiede nome e almeno email o telefono" });
        }

        // Validate winner before any DB writes
        int winnerTeam;
        if (sport.Sets)
        {
            int t1w = req.Sets.Count(s => s.Team1 > s.Team2);
            int t2w = req.Sets.Count(s => s.Team2 > s.Team1);
            if (t1w == t2w)
            {
                int tot1 = req.Sets.Sum(s => s.Team1);
                int tot2 = req.Sets.Sum(s => s.Team2);
                if (tot1 == tot2) return Results.BadRequest(new { error = "La partita deve avere un vincitore (pareggio totale non ammesso)" });
                winnerTeam = tot1 > tot2 ? 1 : 2;
            }
            else winnerTeam = t1w > t2w ? 1 : 2;
        }
        else
        {
            if (req.Sets[0].Team1 == req.Sets[0].Team2)
                return Results.BadRequest(new { error = "La partita deve avere un vincitore (pareggio non ammesso)" });
            winnerTeam = req.Sets[0].Team1 > req.Sets[0].Team2 ? 1 : 2;
        }

        var frontendBase = configuration["Cors:AllowedOrigins:0"] ?? "http://localhost:4200";
        var allSlots = req.Team1.Concat(req.Team2).ToArray();

        if (req.CircleId.HasValue)
        {
            // ── Branch A: use existing circle ──────────────────────────────────
            var circle = await db.Circles.FindAsync(req.CircleId.Value);
            if (circle == null)
                return Results.NotFound(new { error = "Circolo non trovato" });

            var isMember = await db.CircleMemberships
                .AnyAsync(m => m.CircleId == circle.Id && m.UserId == userId);
            if (!isMember)
                return Results.Json(new { error = "Non sei membro del circolo" }, statusCode: 403);

            var resolvedA = await ResolveAllSlotsAsync(allSlots, circle.Id, db);
            if (resolvedA.Distinct().Count() != 4)
                return Results.BadRequest(new { error = "I 4 giocatori devono essere distinti" });

            var matchA      = BuildMatch(circle.Id, userId, resolvedA, winnerTeam);
            var setsA       = BuildSets(matchA.Id, req.Sets);
            var tokensA     = BuildTokens(matchA.Id, userId, resolvedA, frontendBase, out var recipientsA, out var linksA);

            db.Matches.Add(matchA);
            db.MatchSets.AddRange(setsA);
            db.MatchConfirmations.Add(new MatchConfirmation { MatchId = matchA.Id, UserId = userId });
            db.MatchConfirmationTokens.AddRange(tokensA);
            await db.SaveChangesAsync();

            // US-042: nomi+phone per il componente share lato frontend.
            var recipientDataA = await db.Users
                .Where(u => recipientsA.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.Phone, u.IsActivated })
                .ToListAsync();
            var confirmationLinksA = tokensA.Select(t =>
            {
                var u = recipientDataA.First(x => x.Id == t.UserId);
                return new { userId = u.Id, name = u.Name, phone = u.Phone, isActivated = u.IsActivated, tokenUrl = linksA[t.UserId] };
            }).ToList();

            FireNotifications(scopeFactory, loggerFactory, matchA.Id, circle.Id, circle.Name, recipientsA, linksA);

            return Results.Created($"/circles/{circle.Id}/matches/{matchA.Id}", new
            {
                circleId          = circle.Id,
                matchId           = matchA.Id,
                circleName        = circle.Name,
                circleCreated     = false,
                confirmationLinks = confirmationLinksA,
            });
        }
        else
        {
            // ── Branch B: create new circle ────────────────────────────────────
            await using var tx = await db.Database.BeginTransactionAsync();

            var autoName    = await BuildAutoNameAsync(sport.DisplayName, allSlots, db);
            var circleName  = !string.IsNullOrWhiteSpace(req.CircleName) ? req.CircleName.Trim() : autoName;

            var newCircle = new Circle
            {
                OwnerId   = userId,
                Name      = circleName,
                Sport     = sport.Sport,
                PointUnit = sport.PointUnit,
                Sets      = sport.Sets,
                TeamSize  = sport.TeamSize,
                IsPrivate = true,
            };
            db.Circles.Add(newCircle);
            await db.SaveChangesAsync(); // flush to materialise newCircle.Id

            var resolvedB = await ResolveAllSlotsAsync(allSlots, newCircle.Id, db);
            if (resolvedB.Distinct().Count() != 4)
                return Results.BadRequest(new { error = "I 4 giocatori devono essere distinti" });

            var matchB   = BuildMatch(newCircle.Id, userId, resolvedB, winnerTeam);
            var setsB    = BuildSets(matchB.Id, req.Sets);
            var tokensB  = BuildTokens(matchB.Id, userId, resolvedB, frontendBase, out var recipientsB, out var linksB);

            db.Matches.Add(matchB);
            db.MatchSets.AddRange(setsB);
            db.MatchConfirmations.Add(new MatchConfirmation { MatchId = matchB.Id, UserId = userId });
            db.MatchConfirmationTokens.AddRange(tokensB);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            // US-042: nomi+phone per il componente share lato frontend.
            var recipientDataB = await db.Users
                .Where(u => recipientsB.Contains(u.Id))
                .Select(u => new { u.Id, u.Name, u.Phone, u.IsActivated })
                .ToListAsync();
            var confirmationLinksB = tokensB.Select(t =>
            {
                var u = recipientDataB.First(x => x.Id == t.UserId);
                return new { userId = u.Id, name = u.Name, phone = u.Phone, isActivated = u.IsActivated, tokenUrl = linksB[t.UserId] };
            }).ToList();

            FireNotifications(scopeFactory, loggerFactory, matchB.Id, newCircle.Id, newCircle.Name, recipientsB, linksB);

            return Results.Created($"/circles/{newCircle.Id}/matches/{matchB.Id}", new
            {
                circleId          = newCircle.Id,
                matchId           = matchB.Id,
                circleName        = newCircle.Name,
                circleCreated     = true,
                confirmationLinks = confirmationLinksB,
            });
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<Guid[]> ResolveAllSlotsAsync(
        PlayerSlotDto[] slots, Guid circleId, AppDbContext db)
    {
        var ids = new Guid[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot.UserId.HasValue)
            {
                ids[i] = slot.UserId.Value;
                await EnsureMembershipAsync(circleId, slot.UserId.Value, db);
            }
            else
            {
                ids[i] = await ResolveOrCreateGuestAsync(slot, circleId, db);
            }
        }
        return ids;
    }

    private static async Task EnsureMembershipAsync(Guid circleId, Guid memberId, AppDbContext db)
    {
        var trackedAlready = db.CircleMemberships.Local
            .Any(m => m.CircleId == circleId && m.UserId == memberId);
        if (trackedAlready) return;

        var exists = await db.CircleMemberships
            .AnyAsync(m => m.CircleId == circleId && m.UserId == memberId);
        if (!exists)
            db.CircleMemberships.Add(new CircleMembership
            {
                CircleId = circleId,
                UserId   = memberId,
                Rating   = 1000,
            });
    }

    private static async Task<Guid> ResolveOrCreateGuestAsync(
        PlayerSlotDto slot, Guid circleId, AppDbContext db)
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
            db.CircleMemberships.Add(new CircleMembership
            {
                CircleId = circleId,
                UserId   = existing.Id,
                Rating   = 1000,
            });

        return existing.Id;
    }

    private static Match BuildMatch(Guid circleId, Guid userId, Guid[] ids, int winnerTeam) =>
        new()
        {
            CircleId       = circleId,
            CreatedById    = userId,
            WinnerTeam     = winnerTeam,
            Team1Player1Id = ids[0],
            Team1Player2Id = ids[1],
            Team2Player1Id = ids[2],
            Team2Player2Id = ids[3],
        };

    private static List<MatchSet> BuildSets(Guid matchId, SetScoreDto[] sets) =>
        sets.Select((s, i) => new MatchSet
        {
            MatchId    = matchId,
            SetNumber  = i + 1,
            Team1Score = s.Team1,
            Team2Score = s.Team2,
        }).ToList();

    private static List<MatchConfirmationToken> BuildTokens(
        Guid matchId,
        Guid creatorId,
        Guid[] allIds,
        string frontendBase,
        out Guid[] recipientIds,
        out Dictionary<Guid, string> tokenLinks)
    {
        recipientIds = allIds.Where(id => id != creatorId).ToArray();
        var tokens = recipientIds.Select(rid => new MatchConfirmationToken
        {
            MatchId   = matchId,
            UserId    = rid,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(72),
        }).ToList();
        tokenLinks = tokens.ToDictionary(t => t.UserId, t => $"{frontendBase}/m/{t.Token}");
        return tokens;
    }

    private static async Task<string> BuildAutoNameAsync(
        string sportDisplay, PlayerSlotDto[] slots, AppDbContext db)
    {
        async Task<string> NameFor(PlayerSlotDto slot)
        {
            if (!string.IsNullOrWhiteSpace(slot.GuestName)) return slot.GuestName.Trim();
            if (slot.UserId.HasValue)
            {
                var u = await db.Users.FindAsync(slot.UserId.Value);
                return u?.Name ?? "?";
            }
            return "?";
        }

        var n1 = slots.Length > 1 ? await NameFor(slots[1]) : "?";
        var n2 = slots.Length > 2 ? await NameFor(slots[2]) : "?";
        return $"{sportDisplay} con {n1} e {n2}";
    }

    private static void FireNotifications(
        IServiceScopeFactory scopeFactory,
        ILoggerFactory loggerFactory,
        Guid matchId,
        Guid circleId,
        string circleName,
        Guid[] recipientIds,
        Dictionary<Guid, string> tokenLinks)
    {
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
                loggerFactory.CreateLogger(nameof(QuickMatchEndpoints))
                    .LogWarning(ex, "Push dispatch failed for match {MatchId}", matchId);
            }

            var emailLogger = loggerFactory.CreateLogger(nameof(QuickMatchEndpoints));
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                var recipientUsers = await scopedDb.Users
                    .Where(u => recipientIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.Email })
                    .ToListAsync();

                foreach (var r in recipientUsers.Where(r => r.Email != null))
                {
                    var link = tokenLinks.GetValueOrDefault(r.Id, $"http://localhost:4200/circles/{circleId}/matches/{matchId}");
                    try
                    {
                        await emailService.SendMatchConfirmationRequestEmailAsync(r.Email!, circleName, link);
                    }
                    catch (Exception ex)
                    {
                        emailLogger.LogWarning(ex, "Email failed for {Email}, match {MatchId}", r.Email, matchId);
                    }
                }
            }
            catch (Exception ex)
            {
                emailLogger.LogWarning(ex, "Email dispatch failed for match {MatchId}", matchId);
            }
        });
    }
}

record QuickCheckRequest(string? Sport, Guid[]? UserIds, GuestCheckDto[]? Guests);
record GuestCheckDto(string? Email, string? Phone);
record QuickMatchRequest(string? Sport, Guid? CircleId, string? CircleName, PlayerSlotDto[] Team1, PlayerSlotDto[] Team2, SetScoreDto[] Sets);
