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

        return app;
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
