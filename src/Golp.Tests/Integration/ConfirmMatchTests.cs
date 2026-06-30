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

public class ConfirmMatchTests : IClassFixture<ConfirmMatchTestFactory>
{
    private readonly HttpClient _client;
    private readonly ConfirmMatchTestFactory _factory;

    public ConfirmMatchTests(ConfirmMatchTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // CreateMatch → inseritore ha 1 MatchConfirmation implicita
    [Fact]
    public async Task CreateMatch_ImplicitConfirmation_CountIs1()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var matchResp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]);
        Assert.Equal(HttpStatusCode.Created, matchResp.StatusCode);
        var matchId = GetId(await matchResp.Content.ReadFromJsonAsync<JsonElement>());

        var listResp = await _client.GetAsync($"/circles/{circleId}/matches");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var match = list.EnumerateArray().First(m => GetId(m) == matchId);

        Assert.Equal(1, match.GetProperty("confirmationsCount").GetInt32());
        Assert.True(match.GetProperty("hasCurrentUserConfirmed").GetBoolean());
    }

    // 3 conferme progressive → pending; 4ª → confirmed + IRatingService chiamato
    [Fact]
    public async Task ConfirmMatch_4thConfirmation_ConfirmedAndRatingCalled()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchResp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]);
        var matchId = GetId(await matchResp.Content.ReadFromJsonAsync<JsonElement>());

        SetAuth(tokens[1]);
        var r2 = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var b2 = await r2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", b2.GetProperty("status").GetString());
        Assert.Equal(2, b2.GetProperty("confirmationsCount").GetInt32());

        SetAuth(tokens[2]);
        var r3 = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        var b3 = await r3.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", b3.GetProperty("status").GetString());
        Assert.Equal(3, b3.GetProperty("confirmationsCount").GetInt32());

        SetAuth(tokens[3]);
        var r4 = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, r4.StatusCode);
        var b4 = await r4.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("confirmed", b4.GetProperty("status").GetString());
        Assert.Equal(4, b4.GetProperty("confirmationsCount").GetInt32());

        Assert.True(_factory.RatingService.WasCalledWith(matchId));
    }

    // Idempotenza: stessa conferma 2 volte → 200, count invariato
    [Fact]
    public async Task ConfirmMatch_DoubleConfirm_IdempotentCountUnchanged()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        SetAuth(tokens[1]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        var r2 = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var count = (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmationsCount").GetInt32();
        Assert.Equal(2, count);
    }

    // Non-partecipante → 403
    [Fact]
    public async Task ConfirmMatch_NonParticipant_Returns403()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        var outsiderToken = await RegisterTokenAsync();
        SetAuth(outsiderToken);
        await _client.PostAsync($"/circles/{circleId}/join", null);
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // Match confirmed → 409 su ulteriore confirm
    [Fact]
    public async Task ConfirmMatch_AlreadyConfirmed_Returns409()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        var matchId = await CreateAndFullyConfirmAsync(circleId, ids, tokens);

        SetAuth(tokens[0]);
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    // Match disputed → 409 su confirm
    [Fact]
    public async Task ConfirmMatch_AlreadyDisputed_Returns409()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);

        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    // 401 non-autenticato
    [Fact]
    public async Task ConfirmMatch_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var r = await _client.PostAsync($"/circles/{Guid.NewGuid()}/matches/{Guid.NewGuid()}/confirm", null);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    // GET /matches — campi confirmationsCount + hasCurrentUserConfirmed
    [Fact]
    public async Task GetMatches_HasConfirmationFields()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        // user[1] non ha ancora confermato
        SetAuth(tokens[1]);
        var listResp = await _client.GetAsync($"/circles/{circleId}/matches");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var match = list.EnumerateArray().First(m => GetId(m) == matchId);

        Assert.Equal(1, match.GetProperty("confirmationsCount").GetInt32());
        Assert.False(match.GetProperty("hasCurrentUserConfirmed").GetBoolean());

        // user[1] conferma
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        var listResp2 = await _client.GetAsync($"/circles/{circleId}/matches");
        var list2 = await listResp2.Content.ReadFromJsonAsync<JsonElement>();
        var match2 = list2.EnumerateArray().First(m => GetId(m) == matchId);

        Assert.Equal(2, match2.GetProperty("confirmationsCount").GetInt32());
        Assert.True(match2.GetProperty("hasCurrentUserConfirmed").GetBoolean());
    }

    // IRatingService NOT called se match non arriva a 4 conferme
    [Fact]
    public async Task ConfirmMatch_OnlyThreeConfirms_RatingNotCalled()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        SetAuth(tokens[1]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        SetAuth(tokens[2]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);

        Assert.False(_factory.RatingService.WasCalledWith(matchId));
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static Guid GetId(JsonElement el) =>
        Guid.Parse(el.GetProperty("id").GetString()!);

    private async Task<Guid> CreateAndFullyConfirmAsync(Guid circleId, Guid[] ids, string[] tokens)
    {
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        for (var i = 1; i <= 3; i++)
        {
            SetAuth(tokens[i]);
            await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        }

        return matchId;
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

// ─── Test Rating Service ──────────────────────────────────────────────────────

public class TestRatingService : IRatingService
{
    private readonly System.Collections.Concurrent.ConcurrentBag<Guid> _called = [];

    public bool WasCalledWith(Guid matchId) => _called.Contains(matchId);

    public Task<IReadOnlyList<(Guid UserId, int NewPosition)>> CalculateAndApplyAsync(Guid matchId, AppDbContext db)
    {
        _called.Add(matchId);
        return Task.FromResult<IReadOnlyList<(Guid, int)>>([]);
    }
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class ConfirmMatchTestFactory : WebApplicationFactory<Program>
{
    public TestRatingService RatingService { get; } = new();

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
            var dbName = $"ConfirmTestDb_{Guid.NewGuid()}";
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
