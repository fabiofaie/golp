using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Golp.Tests.Integration;

public class PushEndpointsIntegrationTests : IClassFixture<PushTestFactory>
{
    private readonly PushTestFactory _factory;
    private readonly HttpClient _client;

    public PushEndpointsIntegrationTests(PushTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // POST token → 204 + record in DB
    [Fact]
    public async Task RegisterToken_Valid_Returns204AndPersists()
    {
        var (userId, token) = await RegisterUserAsync();
        SetAuth(token);
        var fcmToken = $"fcm_{Guid.NewGuid():N}";

        var resp = await _client.PostAsJsonAsync("/api/push/token",
            new { token = fcmToken, deviceId = "dev-1" });

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.FcmTokens.SingleAsync(t => t.Token == fcmToken);
        Assert.Equal(userId, saved.UserId);
        Assert.Equal("dev-1", saved.DeviceId);
    }

    // POST stesso token 2 volte → 204, 1 solo record (AC: nessun duplicato)
    [Fact]
    public async Task RegisterToken_SameTokenTwice_OnlyOneRecord()
    {
        var (_, token) = await RegisterUserAsync();
        SetAuth(token);
        var fcmToken = $"fcm_{Guid.NewGuid():N}";

        var r1 = await _client.PostAsJsonAsync("/api/push/token", new { token = fcmToken, deviceId = "dev-1" });
        var r2 = await _client.PostAsJsonAsync("/api/push/token", new { token = fcmToken, deviceId = "dev-1" });

        Assert.Equal(HttpStatusCode.NoContent, r1.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, r2.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.FcmTokens.CountAsync(t => t.Token == fcmToken));
    }

    // DELETE → record rimosso
    [Fact]
    public async Task UnregisterToken_RemovesRecord()
    {
        var (_, token) = await RegisterUserAsync();
        SetAuth(token);
        var fcmToken = $"fcm_{Guid.NewGuid():N}";
        await _client.PostAsJsonAsync("/api/push/token", new { token = fcmToken, deviceId = "dev-1" });

        var req = new HttpRequestMessage(HttpMethod.Delete, "/api/push/token")
        {
            Content = JsonContent.Create(new { token = fcmToken })
        };
        var resp = await _client.SendAsync(req);

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.FcmTokens.AnyAsync(t => t.Token == fcmToken));
    }

    // non-autenticato → 401
    [Fact]
    public async Task RegisterToken_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsJsonAsync("/api/push/token",
            new { token = "fcm_x", deviceId = "dev-1" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // token vuoto → 400
    [Fact]
    public async Task RegisterToken_EmptyToken_Returns400()
    {
        var (_, token) = await RegisterUserAsync();
        SetAuth(token);

        var resp = await _client.PostAsJsonAsync("/api/push/token",
            new { token = "", deviceId = "dev-1" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // POST test → 404 se nessun token registrato per l'utente
    [Fact]
    public async Task SendTest_NoTokenRegistered_Returns404()
    {
        var (_, token) = await RegisterUserAsync();
        SetAuth(token);

        var resp = await _client.PostAsync("/api/push/test", null);

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // POST test non-autenticato → 401
    [Fact]
    public async Task SendTest_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.PostAsync("/api/push/test", null);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid UserId, string Token)> RegisterUserAsync()
    {
        var email = $"u_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Player", email, password = "password123" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString()!;
        return (ExtractUserIdFromJwt(token), token);
    }

    private static Guid ExtractUserIdFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

public class PushTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true)
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            services.AddEntityFrameworkInMemoryDatabase();
            var dbName = $"PushTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));
        });

        builder.UseEnvironment("Testing");
    }
}
