using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Golp.Tests.Integration;

// US-037 — GET /circles/{circleId}/matches/{matchId} (dettaglio partita)
public class MatchDetailTests : IClassFixture<MatchDetailTestFactory>
{
    private readonly HttpClient _client;

    public MatchDetailTests(MatchDetailTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    // AC7 — utente non membro del circolo → 403
    [Fact]
    public async Task GetMatchDetail_NonMember_Returns403()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        var outsiderToken = await RegisterTokenAsync();
        SetAuth(outsiderToken);

        var r = await _client.GetAsync($"/circles/{circleId}/matches/{matchId}");
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // AC7 — utente membro del circolo → 200 (comportamento invariato)
    [Fact]
    public async Task GetMatchDetail_Member_Returns200()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        SetAuth(tokens[1]);
        var r = await _client.GetAsync($"/circles/{circleId}/matches/{matchId}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    // AC4, AC5 — confermata dal 4° giocatore: confirmedByName/confirmedAt = 4° confermante, deltas presenti
    [Fact]
    public async Task GetMatchDetail_ConfirmedByFourthPlayer_HasDecisionAndDeltas()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        // ids[0]/tokens[0] è l'owner-inseritore, non partecipa: nessuna conferma implicita.
        // I 4 giocatori sono ids[1..4]; il 4° a confermare è ids[4]/tokens[4].
        SetAuth(tokens[1]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        SetAuth(tokens[2]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        SetAuth(tokens[3]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        SetAuth(tokens[4]);
        var lastConfirm = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, lastConfirm.StatusCode);

        var r = await _client.GetAsync($"/circles/{circleId}/matches/{matchId}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("confirmed", body.GetProperty("status").GetString());
        Assert.True(body.TryGetProperty("confirmedAt", out var confirmedAt) && confirmedAt.ValueKind != JsonValueKind.Null);
        Assert.True(body.TryGetProperty("confirmedByName", out var confirmedByName) && confirmedByName.ValueKind != JsonValueKind.Null);

        Assert.False(body.GetProperty("isForced").GetBoolean());

        var deltas = body.GetProperty("deltas");
        Assert.Equal(4, deltas.GetArrayLength());
        foreach (var d in deltas.EnumerateArray())
            Assert.NotEqual(JsonValueKind.Null, d.GetProperty("delta").ValueKind);
    }

    // AC4, AC5 — confermata per forzatura: confirmedByName = proprietario, confirmedAt = ForceConfirmedAt
    [Fact]
    public async Task GetMatchDetail_ConfirmedByForce_ConfirmedByOwnerWithDeltas()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        var forceResp = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/force-confirm", null);
        Assert.Equal(HttpStatusCode.OK, forceResp.StatusCode);

        SetAuth(tokens[1]);
        var r = await _client.GetAsync($"/circles/{circleId}/matches/{matchId}");
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("confirmed", body.GetProperty("status").GetString());
        Assert.True(body.TryGetProperty("confirmedAt", out var confirmedAt) && confirmedAt.ValueKind != JsonValueKind.Null);
        // l'owner (ids[0]) non è tra i 4 giocatori: il nome va comunque risolto.
        Assert.NotEqual(string.Empty, body.GetProperty("confirmedByName").GetString());
        Assert.True(body.GetProperty("isForced").GetBoolean());

        var deltas = body.GetProperty("deltas");
        Assert.Equal(4, deltas.GetArrayLength());
    }

    // AC6 — pending: confirmedAt/confirmedByName/deltas assenti
    [Fact]
    public async Task GetMatchDetail_Pending_NoDecisionNoDeltas()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        SetAuth(tokens[1]);
        var r = await _client.GetAsync($"/circles/{circleId}/matches/{matchId}");
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("pending", body.GetProperty("status").GetString());
        AssertNullOrAbsent(body, "confirmedAt");
        AssertNullOrAbsent(body, "confirmedByName");
        AssertNullOrAbsent(body, "isForced");
        AssertNullOrAbsent(body, "deltas");
    }

    // AC6 — disputed: confirmedAt/confirmedByName/deltas assenti
    [Fact]
    public async Task GetMatchDetail_Disputed_NoDecisionNoDeltas()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        SetAuth(tokens[1]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);

        var r = await _client.GetAsync($"/circles/{circleId}/matches/{matchId}");
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("disputed", body.GetProperty("status").GetString());
        AssertNullOrAbsent(body, "confirmedAt");
        AssertNullOrAbsent(body, "confirmedByName");
        AssertNullOrAbsent(body, "isForced");
        AssertNullOrAbsent(body, "deltas");
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static void AssertNullOrAbsent(JsonElement body, string property)
    {
        if (body.TryGetProperty(property, out var value))
            Assert.Equal(JsonValueKind.Null, value.ValueKind);
    }

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
        var (p5Id, p5Token) = await RegisterAndJoinAsync(circleId);

        // ids[0] = owner (non partecipa alle partite create con CreatePendingMatchAsync)
        return (circleId,
            new[] { ExtractUserIdFromJwt(ownerToken), p2Id, p3Id, p4Id, p5Id },
            new[] { ownerToken, p2Token, p3Token, p4Token, p5Token });
    }

    private async Task<Guid> CreatePendingMatchAsync(Guid circleId, Guid[] ids)
    {
        // owner (ids[0]) inserisce ma non gioca: 4 giocatori = ids[1..4]
        var resp = await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = ids[1] }, new { userId = ids[2] } },
            team2 = new[] { new { userId = ids[3] }, new { userId = ids[4] } },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
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

public class MatchDetailTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"MatchDetailTestDb_{Guid.NewGuid()}";
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
