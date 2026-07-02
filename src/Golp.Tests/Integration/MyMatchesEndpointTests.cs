using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Golp.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Golp.Tests.Integration;

public class MyMatchesEndpointTests : IClassFixture<MyMatchesTestFactory>
{
    private readonly HttpClient _client;
    private readonly MyMatchesTestFactory _factory;

    public MyMatchesEndpointTests(MyMatchesTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Test 1: non autenticato → 401
    [Fact]
    public async Task GetMyMatches_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/match/mine");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // Test 2: nessuna partita → paged result vuoto
    [Fact]
    public async Task GetMyMatches_NoMatches_ReturnsEmptyPagedResult()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);

        var resp = await _client.GetAsync("/match/mine");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(0, body.GetProperty("items").GetArrayLength());
    }

    // Test 3: partite da due circoli → entrambe in lista
    [Fact]
    public async Task GetMyMatches_ReturnsMatchesAcrossCircles()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        // Circolo 1
        var c1 = await CreateCircleAsync("Padel Roma");
        var (p2, _) = await RegisterAndJoinAsync(c1);
        var (p3, _) = await RegisterAndJoinAsync(c1);
        var (p4, _) = await RegisterAndJoinAsync(c1);
        SetAuth(token);
        await PostMatchAsync(c1, myId, p2, p3, p4);

        // Circolo 2
        SetAuth(token);
        var c2 = await CreateCircleAsync("Beach Ostia");
        var (q2, _) = await RegisterAndJoinAsync(c2);
        var (q3, _) = await RegisterAndJoinAsync(c2);
        var (q4, _) = await RegisterAndJoinAsync(c2);
        SetAuth(token);
        await PostMatchAsync(c2, myId, q2, q3, q4);

        var resp = await _client.GetAsync("/match/mine");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, body.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());

        var items = body.GetProperty("items").EnumerateArray().ToList();
        var circleNames = items.Select(i => i.GetProperty("circleName").GetString()).ToHashSet();
        Assert.Contains("Padel Roma", circleNames);
        Assert.Contains("Beach Ostia", circleNames);
    }

    // Test 4: filtro status=pending → solo pending
    [Fact]
    public async Task GetMyMatches_StatusFilter_ReturnsPendingOnly()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        var c = await CreateCircleAsync("Test Circle");
        var (p2, _) = await RegisterAndJoinAsync(c);
        var (p3, _) = await RegisterAndJoinAsync(c);
        var (p4, _) = await RegisterAndJoinAsync(c);

        // Crea una partita pending e una confermata
        await PostMatchAsync(c, myId, p2, p3, p4);
        var m2Resp = await PostMatchAsync(c, myId, p2, p3, p4);
        var m2Id = GetId(await m2Resp.Content.ReadFromJsonAsync<JsonElement>());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m2 = await db.Matches.FindAsync(m2Id);
        m2!.Status = "confirmed";
        m2.DeltaTeam1Player1 = 10; m2.DeltaTeam1Player2 = 10;
        m2.DeltaTeam2Player1 = -10; m2.DeltaTeam2Player2 = -10;
        await db.SaveChangesAsync();

        var resp = await _client.GetAsync("/match/mine?status=pending");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var items = body.GetProperty("items").EnumerateArray().ToList();
        Assert.All(items, i => Assert.Equal("pending", i.GetProperty("status").GetString()));
    }

    // Test 5: paginazione → seconda pagina restituisce elementi corretti
    [Fact]
    public async Task GetMyMatches_Pagination_SecondPageReturnsCorrectItems()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        var c = await CreateCircleAsync("Pag Circle");
        var (p2, _) = await RegisterAndJoinAsync(c);
        var (p3, _) = await RegisterAndJoinAsync(c);
        var (p4, _) = await RegisterAndJoinAsync(c);

        for (int i = 0; i < 7; i++)
            await PostMatchAsync(c, myId, p2, p3, p4);

        var page1 = await (await _client.GetAsync("/match/mine?page=1&pageSize=5")).Content.ReadFromJsonAsync<JsonElement>();
        var page2 = await (await _client.GetAsync("/match/mine?page=2&pageSize=5")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(7, page1.GetProperty("totalCount").GetInt32());
        Assert.Equal(5, page1.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page2.GetProperty("items").GetArrayLength());
    }

    // Test 6a: myDelta null quando pending
    [Fact]
    public async Task GetMyMatches_MyDeltaNull_WhenPending()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        var c = await CreateCircleAsync("Delta Circle");
        var (p2, _) = await RegisterAndJoinAsync(c);
        var (p3, _) = await RegisterAndJoinAsync(c);
        var (p4, _) = await RegisterAndJoinAsync(c);
        await PostMatchAsync(c, myId, p2, p3, p4);

        var resp = await _client.GetAsync("/match/mine");
        var items = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("myDelta").ValueKind);
    }

    // Test 6b: myDelta valorizzato quando confirmed
    [Fact]
    public async Task GetMyMatches_MyDeltaSet_WhenConfirmed()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        var c = await CreateCircleAsync("Delta2 Circle");
        var (p2, _) = await RegisterAndJoinAsync(c);
        var (p3, _) = await RegisterAndJoinAsync(c);
        var (p4, _) = await RegisterAndJoinAsync(c);
        SetAuth(token);
        var mResp = await PostMatchAsync(c, myId, p2, p3, p4);
        var mId = GetId(await mResp.Content.ReadFromJsonAsync<JsonElement>());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = await db.Matches.FindAsync(mId);
        m!.Status = "confirmed";
        m.DeltaTeam1Player1 = 18; // myId is Team1Player1
        m.DeltaTeam1Player2 = 12;
        m.DeltaTeam2Player1 = -18;
        m.DeltaTeam2Player2 = -12;
        await db.SaveChangesAsync();

        var resp = await _client.GetAsync("/match/mine");
        var items = (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(18, items[0].GetProperty("myDelta").GetInt32());
    }

    // Test 7: team1, team2 e confirmationsCount presenti e corretti
    [Fact]
    public async Task GetMyMatches_ReturnsTeamsAndConfirmationsCount()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        var c = await CreateCircleAsync("Teams Circle");
        var (p2, p2Token) = await RegisterAndJoinAsync(c);
        var (p3, _) = await RegisterAndJoinAsync(c);
        var (p4, p4Token) = await RegisterAndJoinAsync(c);

        SetAuth(token);
        var matchResp = await PostMatchAsync(c, myId, p2, p3, p4);
        var matchId = GetId(await matchResp.Content.ReadFromJsonAsync<JsonElement>());

        // myId è inseritore e partecipante → conferma implicita 1/4
        // p4 conferma → 2/4
        SetAuth(p4Token);
        await _client.PostAsync($"/circles/{c}/matches/{matchId}/confirm", null);

        SetAuth(token);
        var resp = await _client.GetAsync("/match/mine");
        var item = (await resp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray().First();

        // team1 e team2 presenti con 2 giocatori ciascuno
        var team1 = item.GetProperty("team1").EnumerateArray().ToList();
        var team2 = item.GetProperty("team2").EnumerateArray().ToList();
        Assert.Equal(2, team1.Count);
        Assert.Equal(2, team2.Count);

        // ogni giocatore ha userId e name
        Assert.True(team1.All(p => p.TryGetProperty("userId", out _) && p.TryGetProperty("name", out _)));
        Assert.True(team2.All(p => p.TryGetProperty("userId", out _) && p.TryGetProperty("name", out _)));

        // confirmationsCount = 2 (myId implicita + p4)
        Assert.Equal(2, item.GetProperty("confirmationsCount").GetInt32());
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private async Task<string> RegisterTokenAsync()
    {
        var email = $"u_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Player", email, password = "password123" });
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private async Task<Guid> CreateCircleAsync(string name)
    {
        var r = await _client.PostAsJsonAsync("/circles", new { name, sport = "padel" });
        return Guid.Parse((await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);
    }

    private async Task<(Guid Id, string Token)> RegisterAndJoinAsync(Guid circleId)
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        await _client.PostAsync($"/circles/{circleId}/join", null);
        // restore auth to original caller — caller must re-set after this
        return (ExtractUserId(token), token);
    }

    private async Task<HttpResponseMessage> PostMatchAsync(Guid circleId, Guid t1p1, Guid t1p2, Guid t2p1, Guid t2p2)
    {
        return await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = t1p1 }, new { userId = t1p2 } },
            team2 = new[] { new { userId = t2p1 }, new { userId = t2p2 } },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });
    }

    private static Guid GetId(JsonElement el) => Guid.Parse(el.GetProperty("id").GetString()!);

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static Guid ExtractUserId(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class MyMatchesTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"MyMatchesDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.AddSingleton<IRatingService, TestRatingService>();
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
