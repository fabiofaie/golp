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

public class MatchIntegrationTests : IClassFixture<MatchTestFactory>
{
    private readonly HttpClient _client;

    public MatchIntegrationTests(MatchTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    // AC1 — 4 giocatori validi, sets=true → 201 + status pending
    [Fact]
    public async Task CreateMatch_ValidPlayers_SetsTrue_Returns201()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: true);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 }, new { team1 = 3, team2 = 6 }, new { team1 = 7, team2 = 5 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", json.GetProperty("status").GetString());
        Assert.Equal(1, json.GetProperty("winnerTeam").GetInt32());
    }

    // AC2 — sets=false, punteggio singolo → 201
    [Fact]
    public async Task CreateMatch_SetsFalse_SingleScore_Returns201()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 54, team2 = 38 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("winnerTeam").GetInt32());
    }

    // AC6 — status = pending al create
    [Fact]
    public async Task CreateMatch_Status_IsPending()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", json.GetProperty("status").GetString());
    }

    // AC4 — giocatore duplicato → 400
    [Fact]
    public async Task CreateMatch_DuplicatePlayer_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        // ids[0] duplicato in team2
        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[0],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC4 — giocatore non membro → 400
    [Fact]
    public async Task CreateMatch_NonMemberPlayer_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var outsiderId = await RegisterAndExtractIdAsync();

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], outsiderId,
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC3 — inseritore non in nessun team → 400
    [Fact]
    public async Task CreateMatch_CreatorNotInTeam_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();

        var (_, outsiderToken) = await RegisterAndJoinAsync(circleId);
        SetAuth(outsiderToken);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC5 — tie sets → 400 (sets=true, 1-1)
    [Fact]
    public async Task CreateMatch_TieSets_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: true);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 }, new { team1 = 4, team2 = 6 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC5 — tie score → 400 (sets=false)
    [Fact]
    public async Task CreateMatch_TieScore_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 50, team2 = 50 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC2 — sets vuoti → 400
    [Fact]
    public async Task CreateMatch_EmptySets_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var resp = await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { ids[0], ids[1] },
            team2 = new[] { ids[2], ids[3] },
            sets  = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // no auth → 401
    [Fact]
    public async Task CreateMatch_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.PostAsJsonAsync($"/circles/{Guid.NewGuid()}/matches", new
        {
            team1 = new[] { Guid.NewGuid(), Guid.NewGuid() },
            team2 = new[] { Guid.NewGuid(), Guid.NewGuid() },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // circolo non trovato → 404
    [Fact]
    public async Task CreateMatch_CircleNotFound_Returns404()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);

        var resp = await _client.PostAsJsonAsync($"/circles/{Guid.NewGuid()}/matches", new
        {
            team1 = new[] { Guid.NewGuid(), Guid.NewGuid() },
            team2 = new[] { Guid.NewGuid(), Guid.NewGuid() },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // inseritore non membro del circolo → 403
    [Fact]
    public async Task CreateMatch_RequesterNotMember_Returns403()
    {
        var (circleId, ids, _) = await SetupAsync();

        var nonMemberToken = await RegisterTokenAsync();
        SetAuth(nonMemberToken);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // AC2 — sets=false con 2 set → 400
    [Fact]
    public async Task CreateMatch_SetsFalse_MultipleSets_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 54, team2 = 38 }, new { team1 = 60, team2 = 30 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupAsync(bool useSets = true)
    {
        var sport = useSets ? "padel" : "basket2v2";

        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport });
        var circleBody = await circleResp.Content.ReadFromJsonAsync<JsonElement>();
        var circleId = Guid.Parse(circleBody.GetProperty("id").GetString()!);

        var (p2Id, p2Token) = await RegisterAndJoinAsync(circleId);
        var (p3Id, p3Token) = await RegisterAndJoinAsync(circleId);
        var (p4Id, p4Token) = await RegisterAndJoinAsync(circleId);

        var ownerId = ExtractUserIdFromJwt(ownerToken);

        return (circleId,
            new[] { ownerId, p2Id, p3Id, p4Id },
            new[] { ownerToken, p2Token, p3Token, p4Token });
    }

    private async Task<(Guid Id, string Token)> RegisterAndJoinAsync(Guid circleId)
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        await _client.PostAsync($"/circles/{circleId}/join", null);
        return (ExtractUserIdFromJwt(token), token);
    }

    private async Task<Guid> RegisterAndExtractIdAsync()
    {
        var token = await RegisterTokenAsync();
        return ExtractUserIdFromJwt(token);
    }

    private async Task<string> RegisterTokenAsync()
    {
        var email = $"u_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Player", email, password = "password123" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private static Guid ExtractUserIdFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private Task<HttpResponseMessage> PostMatchAsync(Guid circleId,
        Guid t1p1, Guid t1p2, Guid t2p1, Guid t2p2, object sets)
    {
        return _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { t1p1, t1p2 },
            team2 = new[] { t2p1, t2p2 },
            sets,
        });
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

public class MatchTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"MatchTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));
        });

        builder.UseEnvironment("Testing");
    }
}
