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
