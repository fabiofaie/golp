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

public class CircleIntegrationTests : IClassFixture<CircleTestFactory>
{
    private readonly HttpClient _client;

    public CircleIntegrationTests(CircleTestFactory factory)
    {
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
}
