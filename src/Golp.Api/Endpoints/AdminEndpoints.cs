using System.Security.Claims;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/admin").RequireAuthorization();
        admin.MapGet("/whoami", WhoAmIAsync);
        admin.MapPost("/impersonate", ImpersonateAsync);
        admin.MapPost("/impersonate/end", EndImpersonationAsync);
        admin.MapDelete("/circles/{circleId:guid}/matches/{matchId:guid}", DeleteMatchAsync);
        admin.MapPut("/circles/{circleId:guid}/matches/{matchId:guid}/result", EditMatchResultAsync);

        return app;
    }

    // PUT /admin/circles/{circleId}/matches/{matchId}/result — richiede claim super_admin (US-062).
    // Cross-circolo, solo partite confirmed. WinnerTeam sempre ricalcolato server-side dai nuovi set.
    private static async Task<IResult> EditMatchResultAsync(
        Guid circleId,
        Guid matchId,
        EditMatchResultRequest req,
        ClaimsPrincipal user,
        AppDbContext db,
        IRatingService ratingService,
        IGameBonusRatingService gameBonusRatingService)
    {
        if (!user.IsSuperAdmin())
            return Results.Forbid();

        var adminIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (adminIdStr == null || !Guid.TryParse(adminIdStr, out var adminId))
            return Results.Forbid();

        var circle = await db.Circles.FindAsync(circleId);
        if (circle == null)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var match = await db.Matches
            .Include(m => m.Sets)
            .SingleOrDefaultAsync(m => m.Id == matchId && m.CircleId == circleId);
        if (match == null)
            return Results.NotFound(new { error = "Partita non trovata" });

        if (match.Status != "confirmed")
            return Results.Json(new { error = "Solo partite confermate possono essere modificate dal super admin" }, statusCode: 409);

        var (winnerTeamOrNull, setsError) = MatchEndpoints.ValidateSetsAndComputeWinner(req.Sets, circle.Sets);
        if (setsError != null)
            return Results.BadRequest(new { error = setsError });
        int newWinnerTeam = winnerTeamOrNull!.Value;

        var previousSnapshot = System.Text.Json.JsonSerializer.Serialize(new
        {
            match.WinnerTeam,
            Sets = match.Sets.Select(s => new { s.SetNumber, s.Team1Score, s.Team2Score }),
        });
        var newSnapshot = System.Text.Json.JsonSerializer.Serialize(new
        {
            WinnerTeam = newWinnerTeam,
            Sets = req.Sets.Select((s, i) => new { SetNumber = i + 1, Team1Score = s.Team1, Team2Score = s.Team2 }),
        });

        // Transazione reale solo su provider relazionali (SQL Server in produzione), stesso pattern
        // di DeleteMatchAsync: l'in-memory store usato nei test non supporta le transazioni.
        var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;
        try
        {
            db.MatchSets.RemoveRange(match.Sets);
            var newSets = req.Sets.Select((s, i) => new MatchSet
            {
                MatchId    = match.Id,
                SetNumber  = i + 1,
                Team1Score = s.Team1,
                Team2Score = s.Team2,
            }).ToList();
            db.MatchSets.AddRange(newSets);
            match.WinnerTeam = newWinnerTeam;
            await db.SaveChangesAsync();

            // AC4/AC5: circolo Elo o Game+Bonus, entrambi path-dependent per questa storia (a differenza
            // della sola cancellazione) — le partite successive nel circolo furono calcolate con lo
            // snapshot (rating o finestra punteggi) che includeva il vecchio risultato di questa partita.
            if (circle.RatingMethod == "GameBonus")
                await gameBonusRatingService.ResetAndReplayCircleAsync(circleId, db);
            else
                await ratingService.ResetAndReplayCircleAsync(circleId, Guid.NewGuid(), db);

            db.MatchResultEditAuditLogs.Add(new MatchResultEditAuditLog
            {
                SuperAdminId = adminId,
                CircleId = circleId,
                MatchId = matchId,
                PreviousResultJson = previousSnapshot,
                NewResultJson = newSnapshot,
            });
            await db.SaveChangesAsync();

            if (transaction != null)
                await transaction.CommitAsync();
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }

        return Results.Ok(new { id = match.Id, status = match.Status, winnerTeam = match.WinnerTeam });
    }

    // DELETE /admin/circles/{circleId}/matches/{matchId} — richiede claim super_admin (US-061).
    // Cross-circolo: nessun controllo di membership del super admin nel circolo target.
    private static async Task<IResult> DeleteMatchAsync(
        Guid circleId,
        Guid matchId,
        ClaimsPrincipal user,
        AppDbContext db,
        IRatingService ratingService)
    {
        if (!user.IsSuperAdmin())
            return Results.Forbid();

        var adminIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (adminIdStr == null || !Guid.TryParse(adminIdStr, out var adminId))
            return Results.Forbid();

        var circle = await db.Circles.FindAsync(circleId);
        if (circle == null)
            return Results.NotFound(new { error = "Circolo non trovato" });

        var match = await db.Matches
            .Include(m => m.Sets)
            .SingleOrDefaultAsync(m => m.Id == matchId && m.CircleId == circleId);
        if (match == null)
            return Results.NotFound(new { error = "Partita non trovata" });

        var snapshot = System.Text.Json.JsonSerializer.Serialize(new
        {
            match.Status,
            match.WinnerTeam,
            match.Team1Player1Id,
            match.Team1Player2Id,
            match.Team2Player1Id,
            match.Team2Player2Id,
            Sets = match.Sets.Select(s => new { s.SetNumber, s.Team1Score, s.Team2Score }),
        });

        bool needsEloReplay = match.Status == "confirmed" && circle.RatingMethod == "Elo";

        // Transazione reale solo su provider relazionali (SQL Server in produzione): l'in-memory
        // store usato nei test non supporta le transazioni. Garantisce che delete + replay + audit
        // siano atomici (AC6 fail-closed) quando gira su SQL Server.
        var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;
        try
        {
            // La partita va rimossa PRIMA del replay: RatingService.CountKAsync (usato dentro
            // ResetAndReplayCircleAsync) interroga il DB per contare le partite confermate di un
            // giocatore, e una entità solo "tracciata" per la rimozione (Remove senza SaveChanges)
            // resta comunque visibile alle query finché non viene salvata — il match da cancellare
            // gonfierebbe il K-value delle partite successive replayate se non fosse già persistito
            // come rimosso.
            var confirmations = await db.MatchConfirmations.Where(c => c.MatchId == matchId).ToListAsync();
            var confirmationTokens = await db.MatchConfirmationTokens.Where(t => t.MatchId == matchId).ToListAsync();
            db.MatchConfirmations.RemoveRange(confirmations);
            db.MatchConfirmationTokens.RemoveRange(confirmationTokens);
            db.MatchSets.RemoveRange(match.Sets);
            db.Matches.Remove(match);
            await db.SaveChangesAsync();

            // AC3: circolo Elo, partita confirmed → ricostruzione storica dell'intero circolo (path-dependent).
            if (needsEloReplay)
                await ratingService.ResetAndReplayCircleAsync(circleId, matchId, db);
            // Game+Bonus: nessun ricalcolo necessario, la classifica è derivata a runtime (AC4)

            db.MatchDeletionAuditLogs.Add(new MatchDeletionAuditLog
            {
                SuperAdminId = adminId,
                CircleId = circleId,
                MatchId = matchId,
                MatchSnapshotJson = snapshot,
            });
            await db.SaveChangesAsync();

            if (transaction != null)
                await transaction.CommitAsync();
        }
        finally
        {
            if (transaction != null)
                await transaction.DisposeAsync();
        }

        return Results.NoContent();
    }

    // GET /admin/whoami — richiede claim super_admin nel JWT
    private static IResult WhoAmIAsync(ClaimsPrincipal user)
    {
        if (!user.IsSuperAdmin())
            return Results.Forbid();

        var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = user.FindFirstValue(ClaimTypes.Email);

        return Results.Ok(new { userId = userIdStr, email });
    }

    // POST /admin/impersonate — richiede claim super_admin, emette un token per l'utente target
    private static async Task<IResult> ImpersonateAsync(
        ImpersonateRequest req,
        ClaimsPrincipal user,
        AppDbContext db,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        HttpContext httpContext)
    {
        if (!user.IsSuperAdmin())
            return Results.Forbid();

        var adminIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (adminIdStr == null || !Guid.TryParse(adminIdStr, out var adminId))
            return Results.Forbid();

        if (string.IsNullOrWhiteSpace(req.Email))
            return Results.Json(new { error = "Email obbligatoria" }, statusCode: 400);

        var target = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant());
        if (target == null)
            return Results.Json(new { error = "Utente non trovato" }, statusCode: 404);

        // Fail-closed: se il log non si salva, l'impersonazione non parte (garanzia "ogni impersonazione è tracciata")
        db.ImpersonationAuditLogs.Add(new ImpersonationAuditLog { SuperAdminId = adminId, TargetUserId = target.Id });
        await db.SaveChangesAsync();

        var accessToken = jwtService.GenerateToken(target.Id, target.Email, target.SecurityStamp, target.IsSuperAdmin, adminId);
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var refreshToken = await refreshTokenService.IssueAsync(target.Id, userAgent);

        // "token" duplica accessToken per riuso diretto della stessa shape di login/register in AuthService
        return Results.Ok(new { accessToken, refreshToken, token = accessToken });
    }

    // POST /admin/impersonate/end — richiede claim impersonator_id (chiamato mentre autenticati come il target),
    // chiude la riga di audit aperta più recente. Idempotente: 200 anche se nessuna riga aperta viene trovata,
    // perché l'uscita lato client non deve mai essere bloccata da un fallimento qui (best-effort).
    private static async Task<IResult> EndImpersonationAsync(ClaimsPrincipal user, AppDbContext db)
    {
        var impersonatorId = user.GetImpersonatorId();
        if (impersonatorId == null)
            return Results.Forbid();

        var targetIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (targetIdStr == null || !Guid.TryParse(targetIdStr, out var targetId))
            return Results.Forbid();

        var openLog = await db.ImpersonationAuditLogs
            .Where(l => l.SuperAdminId == impersonatorId && l.TargetUserId == targetId && l.EndedAt == null)
            .OrderByDescending(l => l.StartedAt)
            .FirstOrDefaultAsync();

        if (openLog != null)
        {
            openLog.EndedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return Results.Ok(new { });
    }
}

public record ImpersonateRequest(string Email);
public record EditMatchResultRequest(SetScoreDto[] Sets);
