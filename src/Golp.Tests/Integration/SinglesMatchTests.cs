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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Golp.Tests.Integration;

/// <summary>
/// Integration tests for US-047: singles (1v1) match support.
/// </summary>
public class SinglesMatchTests : IClassFixture<SinglesTestFactory>
{
    private readonly SinglesTestFactory _factory;
    private readonly HttpClient _client;

    public SinglesMatchTests(SinglesTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // AC4a — singolo valido su padel (AllowsSingles=true) → 201
    [Fact]
    public async Task CreateMatch_Singles_PadelSport_Returns201()
    {
        var (circleId, ids, tokens) = await SetupSinglesCircleAsync("padel");
        SetAuth(tokens[0]);

        var resp = await PostSinglesMatchAsync(circleId, ids[0], ids[1],
            new[] { new { team1 = 6, team2 = 4 }, new { team1 = 3, team2 = 6 }, new { team1 = 7, team2 = 5 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", json.GetProperty("status").GetString());
    }

    // AC4b — singolo su sport non supportato (basket2v2, AllowsSingles=false) → 400
    [Fact]
    public async Task CreateMatch_Singles_UnsupportedSport_Returns400()
    {
        var (circleId, ids, tokens) = await SetupSinglesCircleAsync("basket2v2");
        SetAuth(tokens[0]);

        var resp = await PostSinglesMatchAsync(circleId, ids[0], ids[1],
            new[] { new { team1 = 21, team2 = 5 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("singolo", json.GetProperty("error").GetString()!);
    }

    // AC4c — isSingles=true ma 2 giocatori per team → 400
    [Fact]
    public async Task CreateMatch_Singles_TwoPlayersPerTeam_Returns400()
    {
        var (circleId, ids, tokens) = await SetupDoublesCircleAsync("padel");
        SetAuth(tokens[0]);

        var resp = await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = ids[0] }, new { userId = ids[1] } },
            team2 = new[] { new { userId = ids[2] }, new { userId = ids[3] } },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
            isSingles = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC5 — singolo: 2 conferme → confirmed (non 4)
    [Fact]
    public async Task ConfirmMatch_Singles_TwoConfirmations_BecomesConfirmed()
    {
        var (circleId, ids, tokens) = await SetupSinglesCircleAsync("padel");
        SetAuth(tokens[0]);

        var createResp = await PostSinglesMatchAsync(circleId, ids[0], ids[1],
            new[] { new { team1 = 6, team2 = 4 } });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var matchId = Guid.Parse((await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        // 1a conferma (ids[0] ha già confermato come creatore — verifico che sia 1/2)
        var detail1 = await GetMatchDetailAsync(circleId, matchId, tokens[0]);
        Assert.Equal("pending", detail1.GetProperty("status").GetString());
        Assert.Equal(1, detail1.GetProperty("confirmationsCount").GetInt32());

        // 2a conferma (ids[1])
        SetAuth(tokens[1]);
        var confResp = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confResp.StatusCode);
        var confJson = await confResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("confirmed", confJson.GetProperty("status").GetString());
    }

    // AC5-reg — doppio: 3 conferme → ancora pending
    [Fact]
    public async Task ConfirmMatch_Doubles_ThreeConfirmations_StillPending()
    {
        var (circleId, ids, tokens) = await SetupDoublesCircleAsync("padel");
        SetAuth(tokens[0]);

        var createResp = await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = ids[0] }, new { userId = ids[1] } },
            team2 = new[] { new { userId = ids[2] }, new { userId = ids[3] } },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var matchId = Guid.Parse((await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        // 2a + 3a conferma
        SetAuth(tokens[1]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        SetAuth(tokens[2]);
        var resp3 = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        var json3 = await resp3.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", json3.GetProperty("status").GetString());
    }

    // AC6 — dopo confirm singolo, solo DeltaTeam1Player1 e DeltaTeam2Player1 non null
    [Fact]
    public async Task ConfirmMatch_Singles_EloUpdatesOnlyTwoPlayers()
    {
        var (circleId, ids, tokens) = await SetupSinglesCircleAsync("padel");
        SetAuth(tokens[0]);

        var createResp = await PostSinglesMatchAsync(circleId, ids[0], ids[1],
            new[] { new { team1 = 6, team2 = 4 } });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var matchId = Guid.Parse((await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        // ids[1] conferma → confermato (2/2)
        SetAuth(tokens[1]);
        var confResp = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confResp.StatusCode);
        Assert.Equal("confirmed", (await confResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());

        // Verifica DB: solo Player1 di team1 e team2 hanno delta non null
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = await db.Matches.FindAsync(matchId);
        Assert.NotNull(match);
        Assert.NotNull(match.DeltaTeam1Player1);
        Assert.Null(match.DeltaTeam1Player2);
        Assert.NotNull(match.DeltaTeam2Player1);
        Assert.Null(match.DeltaTeam2Player2);

        // Rating dei 2 giocatori deve essere cambiato da 1000
        var m1 = await db.CircleMemberships.FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == ids[0]);
        var m2 = await db.CircleMemberships.FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == ids[1]);
        Assert.NotNull(m1);
        Assert.NotNull(m2);
        Assert.NotEqual(1000, m1.Rating);
        Assert.NotEqual(1000, m2.Rating);
    }

    // AC7 — lista partite: team1 e team2 con 1 elemento per singolo
    [Fact]
    public async Task GetMatches_Singles_TeamsHaveOneElement()
    {
        var (circleId, ids, tokens) = await SetupSinglesCircleAsync("padel");
        SetAuth(tokens[0]);

        var createResp = await PostSinglesMatchAsync(circleId, ids[0], ids[1],
            new[] { new { team1 = 6, team2 = 4 } });
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var listResp = await _client.GetAsync($"/circles/{circleId}/matches");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(list);
        Assert.NotEmpty(list);

        var match = list[0];
        Assert.Equal(1, match.GetProperty("team1").GetArrayLength());
        Assert.Equal(1, match.GetProperty("team2").GetArrayLength());
    }

    // AC7 — dettaglio partita singolo: team1 e team2 con 1 elemento
    [Fact]
    public async Task GetMatchDetail_Singles_TeamsHaveOneElement()
    {
        var (circleId, ids, tokens) = await SetupSinglesCircleAsync("padel");
        SetAuth(tokens[0]);

        var createResp = await PostSinglesMatchAsync(circleId, ids[0], ids[1],
            new[] { new { team1 = 6, team2 = 4 } });
        var matchId = Guid.Parse((await createResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        var detail = await GetMatchDetailAsync(circleId, matchId, tokens[0]);
        Assert.Equal(1, detail.GetProperty("team1").GetArrayLength());
        Assert.Equal(1, detail.GetProperty("team2").GetArrayLength());
    }

    // AC2 — GET /sports restituisce allowsSingles per ogni sport
    [Fact]
    public async Task GetSports_ReturnsAllowsSinglesField()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        var resp = await _client.GetAsync("/sports");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var sports = await resp.Content.ReadFromJsonAsync<JsonElement[]>();
        Assert.NotNull(sports);
        Assert.NotEmpty(sports);
        foreach (var s in sports)
            Assert.True(s.TryGetProperty("allowsSingles", out _), "allowsSingles missing from sport");

        var padel = sports.FirstOrDefault(s => s.GetProperty("sport").GetString() == "padel");
        Assert.True(padel.ValueKind != JsonValueKind.Undefined);
        Assert.True(padel.GetProperty("allowsSingles").GetBoolean());

        var basket = sports.FirstOrDefault(s => s.GetProperty("sport").GetString() == "basket2v2");
        Assert.True(basket.ValueKind != JsonValueKind.Undefined);
        Assert.False(basket.GetProperty("allowsSingles").GetBoolean());
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupSinglesCircleAsync(string sport)
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport });
        var circleBody = await circleResp.Content.ReadFromJsonAsync<JsonElement>();
        var circleId = Guid.Parse(circleBody.GetProperty("id").GetString()!);

        var (p2Id, p2Token) = await RegisterAndJoinAsync(circleId);
        var ownerId = ExtractUserIdFromJwt(ownerToken);

        return (circleId,
            new[] { ownerId, p2Id },
            new[] { ownerToken, p2Token });
    }

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupDoublesCircleAsync(string sport)
    {
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

    private Task<HttpResponseMessage> PostSinglesMatchAsync(Guid circleId, Guid p1, Guid p2, object sets)
        => _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = p1 } },
            team2 = new[] { new { userId = p2 } },
            sets,
            isSingles = true,
        });

    private async Task<JsonElement> GetMatchDetailAsync(Guid circleId, Guid matchId, string token)
    {
        SetAuth(token);
        var resp = await _client.GetAsync($"/circles/{circleId}/matches/{matchId}");
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
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

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

public class SinglesTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true
                         || d.ServiceType == typeof(IEmailService))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            services.AddEntityFrameworkInMemoryDatabase();
            var dbName = $"SinglesTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.RemoveAll(typeof(IPushNotificationService));
            services.AddSingleton<IPushNotificationService, RecordingPushNotificationService>();

            services.AddSingleton<TestEmailCapture>();
            services.AddScoped<IEmailService>(sp => sp.GetRequiredService<TestEmailCapture>());
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
