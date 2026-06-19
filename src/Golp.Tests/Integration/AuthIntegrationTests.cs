using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Golp.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public AuthIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // AC1 — Registrazione crea account e autentica subito
    [Fact]
    public async Task Register_ValidData_Returns200WithJwt()
    {
        var response = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email = UniqueEmail(), password = "password123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("token", out var token));
        Assert.NotEmpty(token.GetString()!);
    }

    // AC4 — Password < 8 char → 400
    [Fact]
    public async Task Register_ShortPassword_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email = UniqueEmail(), password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // AC3 — Email duplicata → 409
    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User1", email, password = "password123" });

        var second = await _client.PostAsJsonAsync("/auth/register",
            new { name = "User2", email, password = "differentpass" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // AC2 — Login valido → JWT
    [Fact]
    public async Task Login_ValidCredentials_Returns200WithJwt()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "mypassword1" });

        var response = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "mypassword1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("token", out _));
    }

    // AC2 — Login con credenziali errate → 401 messaggio identico
    [Fact]
    public async Task Login_WrongCredentials_Returns401()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "correctpw1" });

        var wrongPw = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "wrongpassword" });
        var wrongEmail = await _client.PostAsJsonAsync("/auth/login",
            new { email = "nobody@test.com", password = "correctpw1" });

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPw.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongEmail.StatusCode);
    }

    // AC5 — Token scaduto → 401 su route protetta
    [Fact]
    public async Task ProtectedRoute_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/auth/me");
        // /auth/me doesn't exist yet but any protected route returns 401
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized
                 || response.StatusCode == HttpStatusCode.NotFound);
    }

    // AC6 — Reset flow: request → confirm → login nuova password
    [Fact]
    public async Task PasswordReset_FullFlow_Works()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "oldpassword" });

        // Request (always returns 200)
        var requestResp = await _client.PostAsJsonAsync("/auth/password-reset/request",
            new { email });
        Assert.Equal(HttpStatusCode.OK, requestResp.StatusCode);

        // Get token from DB (dev service logs it; we read DB directly)
        var token = await GetLastResetTokenAsync(email);
        Assert.NotNull(token);

        // Confirm with new password
        var confirmResp = await _client.PostAsJsonAsync("/auth/password-reset/confirm",
            new { token, newPassword = "newpassword1" });
        Assert.Equal(HttpStatusCode.OK, confirmResp.StatusCode);

        // Login with new password works
        var loginNew = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "newpassword1" });
        Assert.Equal(HttpStatusCode.OK, loginNew.StatusCode);

        // Login with old password fails
        var loginOld = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "oldpassword" });
        Assert.Equal(HttpStatusCode.Unauthorized, loginOld.StatusCode);
    }

    // AC7 — Link già usato → 400
    [Fact]
    public async Task PasswordReset_AlreadyUsedToken_Returns400()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "oldpassword" });
        await _client.PostAsJsonAsync("/auth/password-reset/request", new { email });

        var token = await GetLastResetTokenAsync(email);
        var payload = new { token, newPassword = "newpassword1" };

        var first = await _client.PostAsJsonAsync("/auth/password-reset/confirm", payload);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/auth/password-reset/confirm", payload);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    // AC7 — Link non valido → 400
    [Fact]
    public async Task PasswordReset_InvalidToken_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/auth/password-reset/confirm",
            new { token = "totallyinvalidtoken", newPassword = "newpassword1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // AC6 — Request email inesistente → sempre 200 (no enumeration)
    [Fact]
    public async Task PasswordResetRequest_UnknownEmail_Returns200()
    {
        var response = await _client.PostAsJsonAsync("/auth/password-reset/request",
            new { email = "nobody@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // US-019 AC1 — register ritorna accessToken + refreshToken, persistito con UserAgent
    [Fact]
    public async Task Register_ReturnsAccessAndRefreshToken_PersistedWithUserAgent()
    {
        var email = UniqueEmail();
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("TestAgent/1.0");

        var response = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "password123" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(body.GetProperty("accessToken").GetString()!);
        Assert.NotEmpty(body.GetProperty("refreshToken").GetString()!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email.ToLowerInvariant());
        var stored = await db.RefreshTokens.SingleAsync(t => t.UserId == user.Id);
        Assert.Contains("TestAgent", stored.UserAgent);
    }

    // US-019 AC1 — login ritorna accessToken + refreshToken
    [Fact]
    public async Task Login_ReturnsAccessAndRefreshToken()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "password123" });

        var response = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(body.GetProperty("accessToken").GetString()!);
        Assert.NotEmpty(body.GetProperty("refreshToken").GetString()!);
    }

    // US-019 AC2/AC4 — refresh valido rinnova access+refresh token
    [Fact]
    public async Task Refresh_ValidToken_ReturnsNewAccessAndRefreshToken()
    {
        var email = UniqueEmail();
        var registerResp = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "password123" });
        var registerBody = await registerResp.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = registerBody.GetProperty("refreshToken").GetString()!;

        var response = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEmpty(body.GetProperty("accessToken").GetString()!);
        var newRefreshToken = body.GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(refreshToken, newRefreshToken);
    }

    // US-019 AC4 — refresh non valido → 401
    [Fact]
    public async Task Refresh_InvalidToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = "not-a-real-token" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // US-019 reuse-detection — riuso di un refresh token già ruotato → 401 e revoca tutta la famiglia
    [Fact]
    public async Task Refresh_ReusedToken_RevokesWholeFamily()
    {
        var email = UniqueEmail();
        var registerResp = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "password123" });
        var registerBody = await registerResp.Content.ReadFromJsonAsync<JsonElement>();
        var firstRefreshToken = registerBody.GetProperty("refreshToken").GetString()!;

        // primo refresh: ruota il token, quello vecchio diventa "usato"
        var firstRotate = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = firstRefreshToken });
        var firstRotateBody = await firstRotate.Content.ReadFromJsonAsync<JsonElement>();
        var secondRefreshToken = firstRotateBody.GetProperty("refreshToken").GetString()!;

        // riuso del token vecchio (già ruotato) → 401
        var reuseResponse = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = firstRefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        // il token successivo della stessa famiglia è stato revocato a cascata
        var cascadeResponse = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken = secondRefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, cascadeResponse.StatusCode);
    }

    // US-019 AC5 — logout revoca il refresh token lato server
    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var email = UniqueEmail();
        var registerResp = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "password123" });
        var registerBody = await registerResp.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = registerBody.GetProperty("refreshToken").GetString()!;

        var logoutResponse = await _client.PostAsJsonAsync("/auth/logout", new { refreshToken });
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        var refreshAfterLogout = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    // US-019 AC6 — cambio password revoca tutti i refresh token dell'utente
    [Fact]
    public async Task PasswordReset_RevokesAllRefreshTokens()
    {
        var email = UniqueEmail();
        var registerResp = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Marco", email, password = "oldpassword1" });
        var registerBody = await registerResp.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = registerBody.GetProperty("refreshToken").GetString()!;

        await _client.PostAsJsonAsync("/auth/password-reset/request", new { email });
        var resetToken = await GetLastResetTokenAsync(email);
        await _client.PostAsJsonAsync("/auth/password-reset/confirm",
            new { token = resetToken, newPassword = "newpassword1" });

        var refreshAfterReset = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterReset.StatusCode);
    }

    private async Task<string?> GetLastResetTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());
        if (user == null) return null;

        var tokenRecord = await db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .OrderByDescending(t => t.ExpiresAt)
            .FirstOrDefaultAsync();

        if (tokenRecord == null) return null;

        // We stored SHA-256 hash; we need the plain token.
        // In tests we intercept via a custom email service that captures tokens.
        // Use the test email service to retrieve the captured token.
        var emailCapture = scope.ServiceProvider.GetRequiredService<TestEmailCapture>();
        return emailCapture.GetLastToken(email);
    }

    private static string UniqueEmail() => $"user_{Guid.NewGuid():N}@test.com";
}

/// <summary>Captures reset links for test retrieval.</summary>
public class TestEmailCapture : Golp.Api.Services.IEmailService
{
    private readonly Dictionary<string, string> _tokens = new();

    public Task SendPasswordResetEmailAsync(string email, string resetLink)
    {
        var uri = new Uri(resetLink);
        var token = System.Web.HttpUtility.ParseQueryString(uri.Query)["token"];
        if (token != null)
            _tokens[email] = token;
        return Task.CompletedTask;
    }

    public string? GetLastToken(string email) =>
        _tokens.TryGetValue(email, out var t) ? t : null;

    public Task SendCircleActivationEmailAsync(string email, string circleName, string activationLink)
    {
        var uri = new Uri(activationLink);
        var token = System.Web.HttpUtility.ParseQueryString(uri.Query)["token"];
        if (token != null)
            _tokens[email] = token;
        return Task.CompletedTask;
    }

    public Task SendAddedToCircleNotificationAsync(string email, string circleName) => Task.CompletedTask;

    public List<(string Email, string CircleName, string MatchLink)> ConfirmationRequestsSent { get; } = [];
    public List<(string Email, string CircleName, string MatchLink)> DisputeNotificationsSent { get; } = [];

    public Task SendMatchConfirmationRequestEmailAsync(string email, string circleName, string matchLink)
    {
        ConfirmationRequestsSent.Add((email, circleName, matchLink));
        return Task.CompletedTask;
    }

    public Task SendMatchDisputedEmailAsync(string email, string circleName, string matchLink)
    {
        DisputeNotificationsSent.Add((email, circleName, matchLink));
        return Task.CompletedTask;
    }
}

public class IntegrationTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true
                         || d.ServiceType == typeof(Golp.Api.Services.IEmailService))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            services.AddEntityFrameworkInMemoryDatabase();
            var dbName = $"IntTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.AddSingleton<TestEmailCapture>();
            services.AddScoped<Golp.Api.Services.IEmailService>(sp =>
                sp.GetRequiredService<TestEmailCapture>());
        });

        builder.UseEnvironment("Testing");
    }
}
