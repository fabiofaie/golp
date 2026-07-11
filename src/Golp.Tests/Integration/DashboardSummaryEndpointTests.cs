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

public class DashboardSummaryEndpointTests : IClassFixture<DashboardSummaryTestFactory>
{
    private readonly HttpClient _client;
    private readonly DashboardSummaryTestFactory _factory;

    public DashboardSummaryEndpointTests(DashboardSummaryTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSummary_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync("/dashboard/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // AC1 — un utente con un circolo e una partita confirmed ottiene tutto in una risposta
    [Fact]
    public async Task GetSummary_SingleCircle_ReturnsActiveCircleWithRecentMatches()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        var c = await CreateCircleAsync("Dash Circle");
        var (p2, _) = await RegisterAndJoinAsync(c);
        var (p3, _) = await RegisterAndJoinAsync(c);
        var (p4, _) = await RegisterAndJoinAsync(c);

        SetAuth(token);
        var matchResp = await PostMatchAsync(c, myId, p2, p3, p4);
        var matchId = GetId(await matchResp.Content.ReadFromJsonAsync<JsonElement>());
        await ConfirmMatchAsync(c, matchId);

        var resp = await _client.GetAsync($"/dashboard/summary?circleId={c}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("circles").GetArrayLength());

        var active = body.GetProperty("activeCircle");
        Assert.Equal("Dash Circle", active.GetProperty("name").GetString());
        Assert.True(active.TryGetProperty("myRating", out _));
        Assert.True(active.TryGetProperty("myRank", out _));
        Assert.Equal(1, active.GetProperty("confirmedMatchesCount").GetInt32());
        Assert.Equal(1, active.GetProperty("recentMatches").GetArrayLength());

        Assert.Equal(JsonValueKind.Null, body.GetProperty("aggregate").ValueKind);
        Assert.Equal(0, body.GetProperty("urgentMatches").GetArrayLength());
    }

    // AC1 — richieste urgenti (pending + disputed) presenti nella stessa risposta
    [Fact]
    public async Task GetSummary_IncludesPendingAndDisputedInUrgentMatches()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        var c = await CreateCircleAsync("Urgent Circle");
        var (p2, p2Token) = await RegisterAndJoinAsync(c);
        var (p3, _) = await RegisterAndJoinAsync(c);
        var (p4, _) = await RegisterAndJoinAsync(c);

        SetAuth(token);
        await PostMatchAsync(c, myId, p2, p3, p4); // resta pending

        var m2Resp = await PostMatchAsync(c, myId, p3, p2, p4);
        var m2Id = GetId(await m2Resp.Content.ReadFromJsonAsync<JsonElement>());
        SetAuth(p2Token);
        await _client.PostAsync($"/circles/{c}/matches/{m2Id}/dispute", null);

        // p2 non ha ancora confermato la pending e ha disputato la seconda: entrambe restano urgenti per lui.
        // (myId, l'inseritore, ha già una conferma implicita su entrambe — la pending non sarebbe più
        // urgente per lui, vedi GetSummary_ExcludesPendingMatchesAlreadyConfirmedByCurrentUser)
        SetAuth(p2Token);
        var resp = await _client.GetAsync($"/dashboard/summary?circleId={c}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var urgent = body.GetProperty("urgentMatches").EnumerateArray().ToList();
        Assert.Equal(2, urgent.Count);
        var statuses = urgent.Select(u => u.GetProperty("status").GetString()).ToHashSet();
        Assert.Contains("pending", statuses);
        Assert.Contains("disputed", statuses);
    }

    // BUGFIX 2026-07-11: un match pending già confermato dall'utente corrente non deve comparire
    // più tra le richieste urgenti (nessuna azione richiesta a lui, sta aspettando gli altri).
    [Fact]
    public async Task GetSummary_ExcludesPendingMatchesAlreadyConfirmedByCurrentUser()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        var c = await CreateCircleAsync("Already Confirmed Circle");
        var (p2, p2Token) = await RegisterAndJoinAsync(c);
        var (p3, _) = await RegisterAndJoinAsync(c);
        var (p4, _) = await RegisterAndJoinAsync(c);

        SetAuth(token);
        var matchResp = await PostMatchAsync(c, myId, p2, p3, p4);
        var matchId = GetId(await matchResp.Content.ReadFromJsonAsync<JsonElement>());
        // myId è inseritore e partecipante -> conferma implicita già presente (1/4)

        var resp = await _client.GetAsync($"/dashboard/summary?circleId={c}");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        // myId ha già confermato (conferma implicita dell'inseritore) -> non è più "urgente" per lui
        Assert.Equal(0, body.GetProperty("urgentMatches").GetArrayLength());

        // per p2 (non ha ancora confermato) il match resta urgente
        SetAuth(p2Token);
        var respP2 = await _client.GetAsync($"/dashboard/summary?circleId={c}");
        var bodyP2 = await respP2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, bodyP2.GetProperty("urgentMatches").GetArrayLength());
        void _() { } // silence unused matchId hint
        _();
    }

    // AC1/US-067 — nessun circleId (modalità "tutti i circoli") popola aggregate, non activeCircle
    [Fact]
    public async Task GetSummary_NoCircleId_ReturnsAggregateNotActiveCircle()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var myId = ExtractUserId(token);

        var c1 = await CreateCircleAsync("Agg Circle 1");
        var (p2, _) = await RegisterAndJoinAsync(c1);
        var (p3, _) = await RegisterAndJoinAsync(c1);
        var (p4, _) = await RegisterAndJoinAsync(c1);
        SetAuth(token);
        var m1 = GetId(await (await PostMatchAsync(c1, myId, p2, p3, p4)).Content.ReadFromJsonAsync<JsonElement>());
        await ConfirmMatchAsync(c1, m1);

        SetAuth(token);
        var c2 = await CreateCircleAsync("Agg Circle 2");

        var resp = await _client.GetAsync("/dashboard/summary");
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Null, body.GetProperty("activeCircle").ValueKind);
        var aggregate = body.GetProperty("aggregate");
        Assert.Equal(2, aggregate.GetProperty("circlesCount").GetInt32());
        Assert.Equal(1, aggregate.GetProperty("confirmedMatchesCount").GetInt32());
        Assert.Equal(100, aggregate.GetProperty("winRate").GetInt32());
        void _() { } // silence unused c2 hint
        _();
    }

    // AC4 — circleId di un circolo di cui l'utente non è membro: fallback ad aggregata, nessun errore
    [Fact]
    public async Task GetSummary_CircleIdNotMember_FallsBackToAggregate_NoError()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var otherCircle = await CreateCircleAsync("Not Mine");

        var myToken = await RegisterTokenAsync();
        SetAuth(myToken);
        await CreateCircleAsync("Mine");

        var resp = await _client.GetAsync($"/dashboard/summary?circleId={otherCircle}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(JsonValueKind.Null, body.GetProperty("activeCircle").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("aggregate").ValueKind);
    }

    // AC4 — circleId malformato: stesso fallback, nessun errore bloccante
    [Fact]
    public async Task GetSummary_MalformedCircleId_FallsBackToAggregate_NoError()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        await CreateCircleAsync("Whatever");

        var resp = await _client.GetAsync("/dashboard/summary?circleId=not-a-guid");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(JsonValueKind.Null, body.GetProperty("activeCircle").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("aggregate").ValueKind);
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

    private async Task ConfirmMatchAsync(Guid circleId, Guid matchId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var m = await db.Matches.FindAsync(matchId);
        m!.Status = "confirmed";
        m.DeltaTeam1Player1 = 18; m.DeltaTeam1Player2 = 12;
        m.DeltaTeam2Player1 = -18; m.DeltaTeam2Player2 = -12;
        await db.SaveChangesAsync();
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

public class DashboardSummaryTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"DashboardSummaryDb_{Guid.NewGuid()}";
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
