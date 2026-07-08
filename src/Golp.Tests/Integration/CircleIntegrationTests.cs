using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Golp.Tests.Integration;

public class CircleIntegrationTests : IClassFixture<CircleTestFactory>
{
    private readonly CircleTestFactory _factory;
    private readonly HttpClient _client;

    public CircleIntegrationTests(CircleTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // GET /sports — returns 4 sports
    [Fact]
    public async Task GetSports_Returns4Sports()
    {
        var response = await _client.GetAsync("/sports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Array, body.ValueKind);
        Assert.Equal(4, body.GetArrayLength());
        var sports = body.EnumerateArray().Select(s => s.GetProperty("sport").GetString()).ToList();
        Assert.Contains("padel",       sports);
        Assert.Contains("beachtennis", sports);
        Assert.Contains("basket2v2",   sports);
        Assert.Contains("burraco",     sports);
    }

    // AC2/AC4 — sport con IsActive=false non deve apparire in GET /sports
    [Fact]
    public async Task GetSports_InactiveSport_IsExcluded()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Sports.Add(new Sport
            {
                Key = "inactive-sport-test", DisplayName = "Inactive", PointUnit = "points",
                Sets = false, TeamSize = 2, IsActive = false, SetWeight = 0.0,
            });
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync("/sports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var sports = body.EnumerateArray().Select(s => s.GetProperty("sport").GetString()).ToList();
        Assert.DoesNotContain("inactive-sport-test", sports);
    }

    // AC1 — crea circolo con nome+sport validi → 200 + body corretto
    [Fact]
    public async Task CreateCircle_ValidData_Returns200WithBody()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/circles",
            new { name = "Test Padel Club", sport = "padel" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test Padel Club", body.GetProperty("name").GetString());
        Assert.Equal("padel",           body.GetProperty("sport").GetString());
        Assert.Equal(1,                 body.GetProperty("memberCount").GetInt32());
    }

    // AC1 — sport non valido → 400
    [Fact]
    public async Task CreateCircle_InvalidSport_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/circles",
            new { name = "Test", sport = "calcio" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // AC2 — config sport assegnata: padel → pointUnit=games, sets=true, teamSize=2
    [Fact]
    public async Task CreateCircle_Padel_HasCorrectSportConfig()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/circles",
            new { name = "Padel Test", sport = "padel" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("games", body.GetProperty("pointUnit").GetString());
        Assert.True(body.GetProperty("sets").GetBoolean());
        Assert.Equal(2, body.GetProperty("teamSize").GetInt32());
    }

    // AC2 — config sport assegnata: basket2v2 → pointUnit=points, sets=false
    [Fact]
    public async Task CreateCircle_Basket2v2_HasCorrectSportConfig()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/circles",
            new { name = "Basket Test", sport = "basket2v2" });

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("points", body.GetProperty("pointUnit").GetString());
        Assert.False(body.GetProperty("sets").GetBoolean());
    }

    // AC3 — creatore è membro con rating 1000
    [Fact]
    public async Task CreateCircle_CreatorIsMemberWithRating1000()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        await _client.PostAsJsonAsync("/circles",
            new { name = "My Circle", sport = "padel" });

        var me = await _client.GetAsync("/circles/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);

        var body = await me.Content.ReadFromJsonAsync<JsonElement>();
        var circles = body.EnumerateArray().ToList();
        Assert.Single(circles);
        Assert.Equal("My Circle", circles[0].GetProperty("name").GetString());
        Assert.Equal(1000,        circles[0].GetProperty("myRating").GetInt32());
        Assert.Equal(1,           circles[0].GetProperty("myRank").GetInt32());
        Assert.Equal(1,           circles[0].GetProperty("memberCount").GetInt32());
    }

    // AC4 — nome vuoto → 400
    [Fact]
    public async Task CreateCircle_EmptyName_Returns400()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync("/circles",
            new { name = "", sport = "padel" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // AC4 — nome duplicato per stesso creatore → 409
    [Fact]
    public async Task CreateCircle_DuplicateName_Returns409()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        await _client.PostAsJsonAsync("/circles",
            new { name = "Dup Circle", sport = "padel" });

        var second = await _client.PostAsJsonAsync("/circles",
            new { name = "Dup Circle", sport = "beachtennis" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // AC4 — stesso nome, creatori diversi → OK (unicità per-creatore)
    [Fact]
    public async Task CreateCircle_SameName_DifferentOwners_BothOk()
    {
        var token1 = await RegisterAndGetTokenAsync();
        var token2 = await RegisterAndGetTokenAsync();

        SetAuth(token1);
        var r1 = await _client.PostAsJsonAsync("/circles",
            new { name = "Same Name", sport = "padel" });

        SetAuth(token2);
        var r2 = await _client.PostAsJsonAsync("/circles",
            new { name = "Same Name", sport = "padel" });

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
    }

    // AC5 — isolamento: utente B non vede circoli utente A
    [Fact]
    public async Task GetMyCircles_IsolationBetweenUsers()
    {
        var tokenA = await RegisterAndGetTokenAsync();
        var tokenB = await RegisterAndGetTokenAsync();

        SetAuth(tokenA);
        await _client.PostAsJsonAsync("/circles",
            new { name = "Alice Circle", sport = "padel" });

        SetAuth(tokenB);
        var me = await _client.GetAsync("/circles/me");
        var body = await me.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    // GET /circles/me — no auth → 401
    [Fact]
    public async Task GetMyCircles_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/circles/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // POST /circles — no auth → 401
    [Fact]
    public async Task CreateCircle_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/circles",
            new { name = "Test", sport = "padel" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Test User", email, password = "password123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// ─── TASK-4: join scenarios ────────────────────────────────────────────────

public class JoinCircleTests : IClassFixture<JoinCircleTestFactory>
{
    private readonly HttpClient _client;
    private readonly JoinCircleTestFactory _factory;

    public JoinCircleTests(JoinCircleTestFactory factory)
    {
        _factory = factory;
        _client  = factory.CreateClient();
    }

    // AC1 — join valido → 200 + utente appare in GET /circles/{id}/members
    [Fact]
    public async Task JoinCircle_ValidId_Returns200_UserAppearsInMembers()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var joinerToken = await RegisterAndGetTokenAsync();
        SetAuth(joinerToken);

        var join = await _client.PostAsync($"/circles/{circleId}/join", null);
        Assert.Equal(HttpStatusCode.OK, join.StatusCode);

        var members = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/members");
        Assert.Equal(JsonValueKind.Array, members.ValueKind);
        Assert.Equal(2, members.GetArrayLength());
    }

    // AC3 — rating iniziale = 1000 al join
    [Fact]
    public async Task JoinCircle_StartsWithRating1000()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var joinerToken = await RegisterAndGetTokenAsync();
        SetAuth(joinerToken);
        await _client.PostAsync($"/circles/{circleId}/join", null);

        var me = await _client.GetFromJsonAsync<JsonElement>("/circles/me");
        var circle = me.EnumerateArray().First(c => c.GetProperty("id").GetString() == circleId.ToString());
        Assert.Equal(1000, circle.GetProperty("myRating").GetInt32());
    }

    // AC2 — multi-circolo: GET /circles/me ritorna entrambi i circoli
    [Fact]
    public async Task JoinCircle_TwoCircles_BothAppearInMyCircles()
    {
        var owner1Token = await RegisterAndGetTokenAsync();
        SetAuth(owner1Token);
        var circle1 = await CreatePublicCircleAsync();

        var owner2Token = await RegisterAndGetTokenAsync();
        SetAuth(owner2Token);
        var circle2 = await CreatePublicCircleAsync();

        var joinerToken = await RegisterAndGetTokenAsync();
        SetAuth(joinerToken);

        await _client.PostAsync($"/circles/{circle1}/join", null);
        await _client.PostAsync($"/circles/{circle2}/join", null);

        var me = await _client.GetFromJsonAsync<JsonElement>("/circles/me");
        Assert.Equal(2, me.GetArrayLength());
    }

    // AC4 — doppio join → 409
    [Fact]
    public async Task JoinCircle_Duplicate_Returns409()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var joinerToken = await RegisterAndGetTokenAsync();
        SetAuth(joinerToken);

        await _client.PostAsync($"/circles/{circleId}/join", null);
        var second = await _client.PostAsync($"/circles/{circleId}/join", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    // circolo inesistente → 404
    [Fact]
    public async Task JoinCircle_CircleNotFound_Returns404()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var response = await _client.PostAsync($"/circles/{Guid.NewGuid()}/join", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // circolo privato → 403
    [Fact]
    public async Task JoinCircle_PrivateCircle_Returns403()
    {
        var ownerToken = await RegisterAndGetTokenAsync("Owner");
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        // Imposta IsPrivate=true direttamente nel DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var c = await db.Circles.FindAsync(circleId);
            c!.IsPrivate = true;
            await db.SaveChangesAsync();
        }

        var joinerToken = await RegisterAndGetTokenAsync("Joiner");
        SetAuth(joinerToken);

        var response = await _client.PostAsync($"/circles/{circleId}/join", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // no auth → 401
    [Fact]
    public async Task JoinCircle_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsync($"/circles/{Guid.NewGuid()}/join", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── helpers ───────────────────────────────────────────────────────────

    private async Task<Guid> CreatePublicCircleAsync()
    {
        var r = await _client.PostAsJsonAsync("/circles",
            new { name = $"Circle_{Guid.NewGuid():N}", sport = "padel" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<string> RegisterAndGetTokenAsync(string namePrefix = "User")
    {
        var email = $"{namePrefix}_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = namePrefix, email, password = "password123" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// ─── TASK-5: members list e discovery ──────────────────────────────────────

public class MembersAndDiscoveryTests : IClassFixture<JoinCircleTestFactory>
{
    private readonly HttpClient _client;

    public MembersAndDiscoveryTests(JoinCircleTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    // AC5 — lista membri mostra solo iscritti al circolo target
    [Fact]
    public async Task GetMembers_ShowsOnlyCircleMembers()
    {
        var ownerAToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerAToken);
        var circleA = await CreatePublicCircleAsync();

        var ownerBToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerBToken);
        var circleB = await CreatePublicCircleAsync();

        // Joiner si unisce solo a circleA
        var joinerToken = await RegisterAndGetTokenAsync();
        SetAuth(joinerToken);
        await _client.PostAsync($"/circles/{circleA}/join", null);

        // circleB deve avere solo ownerB (1 membro)
        SetAuth(ownerBToken);
        var membersB = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleB}/members");
        Assert.Equal(1, membersB.GetArrayLength());
    }

    // AC5 — circolo non trovato → 404
    [Fact]
    public async Task GetMembers_CircleNotFound_Returns404()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var response = await _client.GetAsync($"/circles/{Guid.NewGuid()}/members");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // Discovery — GET /circles restituisce isAlreadyMember corretto
    [Fact]
    public async Task GetCircles_ShowsIsAlreadyMemberFlag()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        await CreatePublicCircleAsync();

        var joinerToken = await RegisterAndGetTokenAsync();
        SetAuth(joinerToken);

        var circles = await _client.GetFromJsonAsync<JsonElement>("/circles");
        Assert.Equal(JsonValueKind.Array, circles.ValueKind);

        // joiner non è ancora membro di nessuno: tutti isAlreadyMember=false
        foreach (var c in circles.EnumerateArray())
            Assert.False(c.GetProperty("isAlreadyMember").GetBoolean());

        // joina il primo e riverifica
        var firstId = circles.EnumerateArray().First().GetProperty("id").GetString();
        await _client.PostAsync($"/circles/{firstId}/join", null);

        var updated = await _client.GetFromJsonAsync<JsonElement>("/circles");
        var joined = updated.EnumerateArray().First(c => c.GetProperty("id").GetString() == firstId);
        Assert.True(joined.GetProperty("isAlreadyMember").GetBoolean());
    }

    // no auth → 401
    [Fact]
    public async Task GetCircles_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync("/circles");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ─── helpers ───────────────────────────────────────────────────────────

    private async Task<Guid> CreatePublicCircleAsync()
    {
        var r = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport = "padel" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"user_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// US-053 — l'endpoint POST /circles/{id}/members è stato rimosso (add-member ridondante
// con invite-link + guest-in-match, vedi AN-002). La route non deve più esistere.
public class AddMemberEndpointTests : IClassFixture<JoinCircleTestFactory>
{
    private readonly JoinCircleTestFactory _factory;
    private readonly HttpClient _client;

    public AddMemberEndpointTests(JoinCircleTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddMember_RouteRemoved_ReturnsNotAllowed()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);
        var circleId = await CreatePublicCircleAsync();

        var response = await _client.PostAsJsonAsync($"/circles/{circleId}/members",
            new { email = "new@test.com", name = "New", confirmed = false });

        // La route GET /{id}/members esiste ancora (lista membri) — POST sullo stesso path
        // non è più mappato, quindi il matcher risponde 405, non 404.
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // ─── helpers ───────────────────────────────────────────────────────────

    private async Task<Guid> CreatePublicCircleAsync()
    {
        var r = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport = "padel" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<string> RegisterAndGetTokenAsync(string? email = null, string name = "User")
    {
        email ??= $"user_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name, email, password = "password123" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// US-038 — test notifiche staff per creazione circolo (usa IntegrationTestFactory che ha TestEmailCapture)
public class CircleStaffNotificationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public CircleStaffNotificationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // US-038 AC2 — staff notification chiamata dopo create circle con successo
    [Fact]
    public async Task CreateCircle_StaffNotificationCalled()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var capture = _factory.Services.GetRequiredService<TestEmailCapture>();
        var beforeCount = capture.NewCircleNotificationsSent.Count;

        var response = await _client.PostAsJsonAsync("/circles",
            new { name = $"Staff_{Guid.NewGuid():N}", sport = "padel" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await capture.WaitUntilCountAsync(() => capture.NewCircleNotificationsSent.Count, beforeCount + 1, TimeSpan.FromSeconds(2));
        Assert.Equal(beforeCount + 1, capture.NewCircleNotificationsSent.Count);
    }

    // US-038 AC3 — errore nella notifica staff non blocca la creazione circolo
    [Fact]
    public async Task CreateCircle_StaffNotificationFails_Returns200()
    {
        var token = await RegisterAndGetTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var capture = _factory.Services.GetRequiredService<TestEmailCapture>();
        capture.ShouldThrowOnStaffNotification = true;
        try
        {
            var response = await _client.PostAsJsonAsync("/circles",
                new { name = $"FailCircle_{Guid.NewGuid():N}", sport = "padel" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            capture.ShouldThrowOnStaffNotification = false;
        }
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email = $"user_{Guid.NewGuid():N}@test.com", password = "password123" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }
}

public class JoinCircleTestFactory : WebApplicationFactory<Program>
{
    public TestEmailCapture EmailCapture { get; } = new();

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
            var dbName = $"JoinTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.AddSingleton(EmailCapture);
            services.AddScoped<Golp.Api.Services.IEmailService>(sp => sp.GetRequiredService<TestEmailCapture>());
        });

        builder.UseEnvironment("Testing");
    }
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }
}

public class CircleTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"CircleTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));
        });

        builder.UseEnvironment("Testing");
    }
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
        return host;
    }
}
