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

namespace Golp.Tests.Integration;

public class MatchListEndpointTests : IClassFixture<MatchListTestFactory>
{
    private readonly HttpClient _client;
    private readonly MatchListTestFactory _factory;

    public MatchListEndpointTests(MatchListTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // GET restituisce array con match del circolo
    [Fact]
    public async Task GetMatches_ReturnsMatchesForCircle()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]);

        var resp = await _client.GetAsync($"/circles/{circleId}/matches");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(list.GetArrayLength() >= 1);
    }

    // myDelta null per partita pending
    [Fact]
    public async Task GetMatches_PendingMatch_MyDeltaIsNull()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]);

        var resp = await _client.GetAsync($"/circles/{circleId}/matches");
        var list = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var match = list.EnumerateArray().First();
        Assert.Equal("pending", match.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, match.GetProperty("myDelta").ValueKind);
    }

    // myDelta valorizzato per utente in team1 su partita confirmed
    [Fact]
    public async Task GetMatches_ConfirmedMatch_MyDeltaIsSetForParticipant()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchResp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]);
        var matchId = GetId(await matchResp.Content.ReadFromJsonAsync<JsonElement>());

        // Imposta delta e status confirmed direttamente nel DB (bypass del full confirm flow)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = await db.Matches.FindAsync(matchId);
        match!.Status = "confirmed";
        match.DeltaTeam1Player1 = 12;
        match.DeltaTeam1Player2 = 8;
        match.DeltaTeam2Player1 = -12;
        match.DeltaTeam2Player2 = -8;
        await db.SaveChangesAsync();

        // tokens[0] è Team1Player1 → myDelta == 12
        SetAuth(tokens[0]);
        var resp = await _client.GetAsync($"/circles/{circleId}/matches");
        var list = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var confirmed = list.EnumerateArray().First(m => GetId(m) == matchId);
        Assert.Equal("confirmed", confirmed.GetProperty("status").GetString());
        Assert.Equal(12, confirmed.GetProperty("myDelta").GetInt32());
    }

    // myDelta null per utente non partecipante (osservatore)
    [Fact]
    public async Task GetMatches_ConfirmedMatch_NonParticipant_MyDeltaIsNull()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchResp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]);
        var matchId = GetId(await matchResp.Content.ReadFromJsonAsync<JsonElement>());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = await db.Matches.FindAsync(matchId);
        match!.Status = "confirmed";
        match.DeltaTeam1Player1 = 12;
        match.DeltaTeam1Player2 = 8;
        match.DeltaTeam2Player1 = -12;
        match.DeltaTeam2Player2 = -8;
        await db.SaveChangesAsync();

        // Registra un quinto utente (osservatore) che si unisce al circolo
        var observerToken = await RegisterTokenAsync();
        SetAuth(observerToken);
        await _client.PostAsync($"/circles/{circleId}/join", null);

        var resp = await _client.GetAsync($"/circles/{circleId}/matches");
        var list = await resp.Content.ReadFromJsonAsync<JsonElement>();

        var confirmed = list.EnumerateArray().First(m => GetId(m) == matchId);
        Assert.Equal(JsonValueKind.Null, confirmed.GetProperty("myDelta").ValueKind);
    }

    // La risposta non espone campi di calcolo intermedio (algoritmo opaco)
    [Fact]
    public async Task GetMatches_ResponseDoesNotContainFormulaFields()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]);

        var resp = await _client.GetAsync($"/circles/{circleId}/matches");
        var json = await resp.Content.ReadAsStringAsync();

        Assert.DoesNotContain("expectedScore", json);
        Assert.DoesNotContain("teamRating", json);
    }

    // 401 non autenticato
    [Fact]
    public async Task GetMatches_Unauthenticated_Returns401()
    {
        var (circleId, _, _) = await SetupAsync();
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.GetAsync($"/circles/{circleId}/matches");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // 404 circolo inesistente
    [Fact]
    public async Task GetMatches_NonExistentCircle_Returns404()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);

        var resp = await _client.GetAsync($"/circles/{Guid.NewGuid()}/matches");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupAsync()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse((await circleResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        var (p2Id, p2Token) = await RegisterAndJoinAsync(circleId);
        var (p3Id, p3Token) = await RegisterAndJoinAsync(circleId);
        var (p4Id, p4Token) = await RegisterAndJoinAsync(circleId);

        return (circleId,
            new[] { ExtractUserIdFromJwt(ownerToken), p2Id, p3Id, p4Id },
            new[] { ownerToken, p2Token, p3Token, p4Token });
    }

    private async Task<(Guid Id, string Token)> RegisterAndJoinAsync(Guid circleId)
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        await _client.PostAsync($"/circles/{circleId}/join", null);
        return (ExtractUserIdFromJwt(token), token);
    }

    private async Task<string> RegisterTokenAsync()
    {
        var email = $"u_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Player", email, password = "password123" });
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private Task<HttpResponseMessage> PostMatchAsync(Guid circleId, Guid t1p1, Guid t1p2, Guid t2p1, Guid t2p2)
    {
        return _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { t1p1, t1p2 },
            team2 = new[] { t2p1, t2p2 },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });
    }

    private static Guid GetId(JsonElement el) => Guid.Parse(el.GetProperty("id").GetString()!);

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static Guid ExtractUserIdFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class MatchListTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"MatchListDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.AddSingleton<IRatingService, TestRatingService>();
        });

        builder.UseEnvironment("Testing");
    }
}
