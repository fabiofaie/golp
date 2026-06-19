using System.ComponentModel.DataAnnotations;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapPost("/logout", LogoutAsync);
        group.MapPost("/password-reset/request", RequestPasswordResetAsync);
        group.MapPost("/password-reset/confirm", ConfirmPasswordResetAsync);

        return app;
    }

    // POST /auth/register
    private static async Task<IResult> RegisterAsync(
        RegisterRequest req,
        AppDbContext db,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest(new { error = "Il nome è obbligatorio" });

        if (!IsValidEmail(req.Email))
            return Results.BadRequest(new { error = "Formato email non valido" });

        if (req.Password.Length < 8)
            return Results.BadRequest(new { error = "La password deve essere di almeno 8 caratteri" });

        if (await db.Users.AnyAsync(u => u.Email == req.Email.ToLowerInvariant()))
            return Results.Conflict(new { error = "Email già registrata" });

        var user = new User
        {
            Name = req.Name.Trim(),
            Email = req.Email.ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password)
        };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var accessToken = jwtService.GenerateToken(user.Id, user.Email);
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var refreshToken = await refreshTokenService.IssueAsync(user.Id, userAgent);

        return Results.Ok(new { accessToken, refreshToken, token = accessToken });
    }

    // POST /auth/login
    private static async Task<IResult> LoginAsync(
        LoginRequest req,
        AppDbContext db,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        HttpContext httpContext)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant());
        if (user == null || string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Results.Json(new { error = "Credenziali non valide" }, statusCode: 401);

        var accessToken = jwtService.GenerateToken(user.Id, user.Email);
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var refreshToken = await refreshTokenService.IssueAsync(user.Id, userAgent);

        return Results.Ok(new { accessToken, refreshToken, token = accessToken });
    }

    // POST /auth/refresh
    private static async Task<IResult> RefreshAsync(
        RefreshRequest req,
        AppDbContext db,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        HttpContext httpContext)
    {
        var userAgent = httpContext.Request.Headers.UserAgent.ToString();
        var result = await refreshTokenService.RotateAsync(req.RefreshToken, userAgent);

        if (!result.Success)
            return Results.Json(new { error = "Refresh token non valido o scaduto" }, statusCode: 401);

        var user = await db.Users.FindAsync(result.UserId);
        if (user == null)
            return Results.Json(new { error = "Refresh token non valido o scaduto" }, statusCode: 401);

        var accessToken = jwtService.GenerateToken(user.Id, user.Email);
        return Results.Ok(new { accessToken, refreshToken = result.NewPlainToken });
    }

    // POST /auth/logout
    private static async Task<IResult> LogoutAsync(
        RefreshRequest req,
        IRefreshTokenService refreshTokenService)
    {
        await refreshTokenService.RevokeAsync(req.RefreshToken);
        return Results.Ok();
    }

    // POST /auth/password-reset/request
    private static async Task<IResult> RequestPasswordResetAsync(
        PasswordResetRequestRequest req,
        AppDbContext db,
        IPasswordResetService resetService,
        IEmailService emailService,
        IConfiguration configuration)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email.ToLowerInvariant());

        if (user != null)
        {
            var plainToken = await resetService.GenerateTokenAsync(user.Id);
            var frontendBase = configuration["Cors:AllowedOrigins:0"] ?? "http://localhost:4200";
            var resetLink = $"{frontendBase}/reset-password?token={Uri.EscapeDataString(plainToken)}";
            await emailService.SendPasswordResetEmailAsync(user.Email, resetLink);
        }

        return Results.Ok();
    }

    // POST /auth/password-reset/confirm
    private static async Task<IResult> ConfirmPasswordResetAsync(
        PasswordResetConfirmRequest req,
        AppDbContext db,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService)
    {
        if (req.NewPassword.Length < 8)
            return Results.BadRequest(new { error = "La password deve essere di almeno 8 caratteri" });

        var tokenHash = PasswordResetService.ComputeSha256(req.Token);

        var resetToken = await db.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

        if (resetToken == null)
            return Results.BadRequest(new { error = "Link non valido" });

        if (resetToken.UsedAt != null)
            return Results.BadRequest(new { error = "Link già utilizzato" });

        if (resetToken.ExpiresAt < DateTime.UtcNow)
            return Results.BadRequest(new { error = "Link scaduto" });

        resetToken.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        resetToken.UsedAt = DateTime.UtcNow;

        await refreshTokenService.RevokeAllForUserAsync(resetToken.UserId);

        await db.SaveChangesAsync();
        return Results.Ok();
    }

    private static bool IsValidEmail(string email) =>
        new EmailAddressAttribute().IsValid(email);
}

// Request DTOs
record RegisterRequest(string Name, string Email, string Password);
record LoginRequest(string Email, string Password);
record RefreshRequest(string RefreshToken);
record PasswordResetRequestRequest(string Email);
record PasswordResetConfirmRequest(string Token, string NewPassword);
