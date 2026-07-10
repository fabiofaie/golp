using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Golp.Tests.Integration;

public class PublicMatchTokenTests : IClassFixture<PublicMatchTokenFactory>
{
    private readonly HttpClient _client;
    private readonly PublicMatchTokenFactory _factory;

    public PublicMatchTokenTests(PublicMatchTokenFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── AC1+AC2: creazione partita genera token per i 3 non-creator ─────────

    [Fact]
    public async Task CreateMatch_GeneratesTokensForNonCreatorPlayers()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var matchResp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]);
        Assert.Equal(HttpStatusCode.Created, matchResp.StatusCode);
        var matchId = GetId(await matchResp.Content.ReadFromJsonAsync<JsonElement>());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tokenCount = await db.MatchConfirmationTokens.CountAsync(t => t.MatchId == matchId);
        Assert.Equal(3, tokenCount);

        // Creator (ids[0]) non ha token
        var creatorHasToken = await db.MatchConfirmationTokens.AnyAsync(t => t.MatchId == matchId && t.UserId == ids[0]);
        Assert.False(creatorHasToken);

        // Token hanno scadenza 72h
        var anyExpiredEarly = await db.MatchConfirmationTokens
            .AnyAsync(t => t.MatchId == matchId && t.ExpiresAt < DateTimeOffset.UtcNow.AddHours(70));
        Assert.False(anyExpiredEarly);
    }

    // ─── AC3: GET /m/{token} valido → 200 con dati partita ───────────────────

    [Fact]
    public async Task GetPublicMatch_ValidToken_Returns200WithMatchSummary()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        var token = await GetTokenForUserAsync(matchId, ids[1]);

        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync($"/m/{token}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(json.GetProperty("tokenUsed").GetBoolean());
        Assert.True(json.GetProperty("token").GetProperty("valid").GetBoolean());
        Assert.Equal("pending", json.GetProperty("match").GetProperty("status").GetString());
        Assert.Equal(1, json.GetProperty("match").GetProperty("confirmationsCount").GetInt32());
        Assert.True(json.GetProperty("match").TryGetProperty("team1", out _));
    }

    // ─── Token inesistente → 404 ──────────────────────────────────────────────

    [Fact]
    public async Task GetPublicMatch_InvalidToken_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync($"/m/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─── Token scaduto → 410 ─────────────────────────────────────────────────

    [Fact]
    public async Task GetPublicMatch_ExpiredToken_Returns410()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        var tokenValue = await ExpireTokenForUserAsync(matchId, ids[1]);

        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.GetAsync($"/m/{tokenValue}");
        Assert.Equal(410, (int)resp.StatusCode);
    }

    // ─── AC5+AC8: 4 confirm via token → confirmed + rating applicato ─────────

    [Fact]
    public async Task ConfirmViaToken_ValidToken_ConfirmsMatch_And_UpdatesRating()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        _client.DefaultRequestHeaders.Authorization = null;

        // 3 giocatori non-creator confermano via token
        for (var i = 1; i <= 3; i++)
        {
            var t = await GetTokenForUserAsync(matchId, ids[i]);
            var r = await _client.PostAsync($"/m/{t}/confirm", null);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            var body = await r.Content.ReadFromJsonAsync<JsonElement>();

            if (i == 3)
            {
                Assert.Equal("confirmed", body.GetProperty("status").GetString());
                Assert.True(_factory.RatingService.WasCalledWith(matchId));
            }
            else
            {
                Assert.Equal("pending", body.GetProperty("status").GetString());
            }
        }
    }

    // ─── AC6: token già usato → alreadyDone (idempotente) ───────────────────

    [Fact]
    public async Task ConfirmViaToken_AlreadyUsedToken_ReturnsAlreadyDone()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        _client.DefaultRequestHeaders.Authorization = null;
        var token = await GetTokenForUserAsync(matchId, ids[1]);

        await _client.PostAsync($"/m/{token}/confirm", null);
        var r2 = await _client.PostAsync($"/m/{token}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var body = await r2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("alreadyDone").GetBoolean());
    }

    // ─── AC5: disputa via token ───────────────────────────────────────────────

    [Fact]
    public async Task DisputeViaToken_ValidToken_DisputesMatch()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        _client.DefaultRequestHeaders.Authorization = null;
        var token = await GetTokenForUserAsync(matchId, ids[1]);

        var resp = await _client.PostAsync($"/m/{token}/dispute", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("disputed", body.GetProperty("status").GetString());
    }

    // ─── Match già confermata → 409 ──────────────────────────────────────────

    [Fact]
    public async Task ConfirmViaToken_MatchAlreadyConfirmed_Returns409()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        // Conferma autenticata 3 volte (creator è già 1)
        for (var i = 1; i <= 3; i++)
        {
            SetAuth(tokens[i]);
            await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        }

        // Ora prova via token (il token non è stato ancora usato, ma la partita è confermata)
        _client.DefaultRequestHeaders.Authorization = null;
        var token = await GetTokenForUserAsync(matchId, ids[1]);
        var resp = await _client.PostAsync($"/m/{token}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    // ─── CTA: isActivated false per ospite ───────────────────────────────────

    [Fact]
    public async Task ConfirmViaToken_GuestUser_ReturnsIsActivatedFalse()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse((await circleResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        var (p2Id, _) = await RegisterAndJoinAsync(circleId);
        var (p3Id, _) = await RegisterAndJoinAsync(circleId);

        // Slot ospite in team2
        var guestEmail = $"guest_{Guid.NewGuid():N}@test.com";
        var ownerId = ExtractUserIdFromJwt(ownerToken);
        var matchResp = await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new object[] { new { userId = ownerId }, new { userId = p2Id } },
            team2 = new object[] { new { userId = p3Id }, new { guestName = "Ospite Test", guestEmail } },
            sets = new[] { new { team1 = 6, team2 = 4 } },
        });
        Assert.Equal(HttpStatusCode.Created, matchResp.StatusCode);
        var matchId = GetId(await matchResp.Content.ReadFromJsonAsync<JsonElement>());

        // Trova l'utente ospite e il suo token
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var guestUser = await db.Users.FirstAsync(u => u.Email == guestEmail);
        Assert.False(guestUser.IsActivated);

        var tokenValue = await GetTokenForUserAsync(matchId, guestUser.Id);
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.PostAsync($"/m/{tokenValue}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("isActivated").GetBoolean());
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static Guid GetId(JsonElement el) =>
        Guid.Parse(el.GetProperty("id").GetString()!);

    private async Task<Guid> GetTokenForUserAsync(Guid matchId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = await db.MatchConfirmationTokens.FirstAsync(t => t.MatchId == matchId && t.UserId == userId);
        return t.Token;
    }

    private async Task<Guid> ExpireTokenForUserAsync(Guid matchId, Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = await db.MatchConfirmationTokens.FirstAsync(t => t.MatchId == matchId && t.UserId == userId);
        t.ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
        return t.Token;
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

    private static Guid ExtractUserIdFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private Task<HttpResponseMessage> PostMatchAsync(Guid circleId, Guid t1p1, Guid t1p2, Guid t2p1, Guid t2p2) =>
        _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = t1p1 }, new { userId = t1p2 } },
            team2 = new[] { new { userId = t2p1 }, new { userId = t2p2 } },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// ─── Rating service stub ──────────────────────────────────────────────────────

public class PublicMatchRatingService : IRatingService
{
    private readonly System.Collections.Concurrent.ConcurrentBag<Guid> _called = [];
    public bool WasCalledWith(Guid matchId) => _called.Contains(matchId);
    public Task<IReadOnlyList<(Guid UserId, int NewPosition)>> CalculateAndApplyAsync(Guid matchId, AppDbContext db)
    {
        _called.Add(matchId);
        return Task.FromResult<IReadOnlyList<(Guid, int)>>([]);
    }

    public Task ResetAndReplayCircleAsync(Guid circleId, Guid excludeMatchId, AppDbContext db) => Task.CompletedTask;
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class PublicMatchTokenFactory : WebApplicationFactory<Program>
{
    public PublicMatchRatingService RatingService { get; } = new();

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
            var dbName = $"PublicMatchTokenDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.AddSingleton<IRatingService>(RatingService);
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
