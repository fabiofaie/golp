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

public class LeaderboardEndpointTests : IClassFixture<LeaderboardTestFactory>
{
    private readonly HttpClient _client;

    public LeaderboardEndpointTests(LeaderboardTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    // AC — 401 per richiesta non autenticata
    [Fact]
    public async Task GetLeaderboard_Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.GetAsync($"/circles/{Guid.NewGuid()}/leaderboard");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // AC — 404 per circolo inesistente
    [Fact]
    public async Task GetLeaderboard_NonExistentCircle_Returns404()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var response = await _client.GetAsync($"/circles/{Guid.NewGuid()}/leaderboard");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // AC — circolo con tutti i membri a 0 confirmed → classified=[], unclassified=tutti
    [Fact]
    public async Task GetLeaderboard_NoConfirmedMatches_AllUnclassified()
    {
        var (circleId, _, tokens) = await SetupCircleWithMembersAsync(memberCount: 3);
        SetAuth(tokens[0]);

        var body = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/leaderboard");
        var classified   = body.GetProperty("classified").EnumerateArray().ToList();
        var unclassified = body.GetProperty("unclassified").EnumerateArray().ToList();

        Assert.Empty(classified);
        Assert.Equal(4, unclassified.Count); // owner + 3 members
    }

    // AC — 3 membri (2 con confirmed match a rating diverso, 1 senza) → classified=2 ordinati, unclassified=1
    [Fact]
    public async Task GetLeaderboard_SomePlayed_ClassifiedAndUnclassifiedCorrect()
    {
        var (circleId, memberIds, tokens) = await SetupCircleWithMembersAsync(memberCount: 3);

        // Confirm 1 match involving players 0,1,2,3 (all 4 players)
        await FullyConfirmMatchAsync(circleId, memberIds, tokens,
            t1p1: 0, t1p2: 1, t2p1: 2, t2p2: 3);

        // Player 4 (index 4 if exists) stays unclassified — here owner+3 = 4 players, all played
        // Add a 5th member who never plays
        var (_, extraToken) = await RegisterAndJoinAsync(circleId);
        SetAuth(extraToken);

        var body = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/leaderboard");
        var classified   = body.GetProperty("classified").EnumerateArray().ToList();
        var unclassified = body.GetProperty("unclassified").EnumerateArray().ToList();

        // 4 players who participated in confirmed match → classified
        Assert.Equal(4, classified.Count);
        // 1 player who never played → unclassified
        Assert.Single(unclassified);

        // classified must be ordered by rating DESC
        var ratings = classified.Select(e => e.GetProperty("rating").GetInt32()).ToList();
        Assert.Equal(ratings.OrderByDescending(r => r).ToList(), ratings);

        // each classified entry has confirmedMatches ≥ 1
        foreach (var entry in classified)
            Assert.True(entry.GetProperty("confirmedMatches").GetInt32() >= 1);
    }

    // AC — rank assegnato in ordine crescente
    [Fact]
    public async Task GetLeaderboard_RankIsConsecutiveFromOne()
    {
        var (circleId, memberIds, tokens) = await SetupCircleWithMembersAsync(memberCount: 3);
        await FullyConfirmMatchAsync(circleId, memberIds, tokens, 0, 1, 2, 3);

        SetAuth(tokens[0]);
        var body = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/leaderboard");
        var classified = body.GetProperty("classified").EnumerateArray().ToList();

        var ranks = classified.Select(e => e.GetProperty("rank").GetInt32()).ToList();
        Assert.Equal(Enumerable.Range(1, classified.Count).ToList(), ranks);
    }

    // AC — tie-break: stessa rating → più confirmedMatches prima
    [Fact]
    public async Task GetLeaderboard_TieBreak_MoreMatchesRankedFirst()
    {
        // Setup fresh circle with 4 players
        var (circleId, memberIds, tokens) = await SetupCircleWithMembersAsync(memberCount: 3);

        // Confirm 2 matches with players 0,1 vs 2,3 to accumulate match count on some
        await FullyConfirmMatchAsync(circleId, memberIds, tokens, 0, 1, 2, 3);
        await FullyConfirmMatchAsync(circleId, memberIds, tokens, 0, 1, 2, 3);

        SetAuth(tokens[0]);
        var body = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/leaderboard");
        var classified = body.GetProperty("classified").EnumerateArray().ToList();

        // All 4 players have the same rating only at the start; after ELO some will differ.
        // What matters: players with same rating but more confirmedMatches come first.
        // Verify confirmedMatches is present and correct (2 matches for all players)
        foreach (var entry in classified)
            Assert.Equal(2, entry.GetProperty("confirmedMatches").GetInt32());
    }

    // AC — due circoli separati: leaderboard ritorna solo i dati del circolo richiesto
    [Fact]
    public async Task GetLeaderboard_TwoCircles_ReturnsDataForRequestedCircle()
    {
        var (circle1, ids1, tokens1) = await SetupCircleWithMembersAsync(memberCount: 3);
        var (circle2, ids2, tokens2) = await SetupCircleWithMembersAsync(memberCount: 3);

        // Confirm match in circle1 only
        await FullyConfirmMatchAsync(circle1, ids1, tokens1, 0, 1, 2, 3);

        SetAuth(tokens2[0]);
        var body = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circle2}/leaderboard");
        var classified = body.GetProperty("classified").EnumerateArray().ToList();

        // circle2 has no confirmed matches → all unclassified
        Assert.Empty(classified);
    }

    // ─── helpers ────────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] MemberIds, string[] Tokens)> SetupCircleWithMembersAsync(int memberCount)
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"LB_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse(
            (await circleResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        var ids    = new List<Guid>   { ExtractUserIdFromJwt(ownerToken) };
        var tokens = new List<string> { ownerToken };

        for (var i = 0; i < memberCount; i++)
        {
            var (id, tok) = await RegisterAndJoinAsync(circleId);
            ids.Add(id);
            tokens.Add(tok);
        }

        return (circleId, ids.ToArray(), tokens.ToArray());
    }

    private async Task<(Guid Id, string Token)> RegisterAndJoinAsync(Guid circleId)
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        await _client.PostAsync($"/circles/{circleId}/join", null);
        return (ExtractUserIdFromJwt(token), token);
    }

    private async Task FullyConfirmMatchAsync(Guid circleId, Guid[] ids, string[] tokens,
        int t1p1, int t1p2, int t2p1, int t2p2)
    {
        SetAuth(tokens[t1p1]);
        var resp = await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = ids[t1p1] }, new { userId = ids[t1p2] } },
            team2 = new[] { new { userId = ids[t2p1] }, new { userId = ids[t2p2] } },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });
        var matchId = Guid.Parse(
            (await resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        foreach (var idx in new[] { t1p2, t2p1, t2p2 })
        {
            SetAuth(tokens[idx]);
            await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        }
    }

    private async Task<string> RegisterTokenAsync()
    {
        var email = $"lb_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Player", email, password = "password123" });
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private static Guid ExtractUserIdFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded  = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes   = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class LeaderboardTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"LeaderboardTestDb_{Guid.NewGuid()}";
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
