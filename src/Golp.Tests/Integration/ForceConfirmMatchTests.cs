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

public class ForceConfirmMatchTests : IClassFixture<ForceConfirmTestFactory>
{
    private readonly HttpClient _client;
    private readonly ForceConfirmTestFactory _factory;

    public ForceConfirmMatchTests(ForceConfirmTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_ForceConfirm_Pending_Returns200AndConfirmed()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/force-confirm", null);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("confirmed", body.GetProperty("status").GetString());
        Assert.Equal(ids[0].ToString(), body.GetProperty("forceConfirmedBy").GetString());
    }

    [Fact]
    public async Task Owner_ForceConfirm_Pending_RatingServiceCalled()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/force-confirm", null);

        Assert.True(_factory.RatingService.WasCalledWith(matchId));
    }

    [Fact]
    public async Task Owner_ForceConfirm_Pending_AuditFieldsInResponse()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/force-confirm", null);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(ids[0].ToString(), body.GetProperty("forceConfirmedBy").GetString());
    }

    [Fact]
    public async Task NonOwnerMember_ForceConfirm_Returns403()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        SetAuth(tokens[1]);
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/force-confirm", null);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task NonMember_ForceConfirm_Returns403()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        var outsiderToken = await RegisterTokenAsync();
        SetAuth(outsiderToken);
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/force-confirm", null);

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Owner_ForceConfirm_AlreadyConfirmed_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        // prima forza la conferma
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/force-confirm", null);

        // poi riprova
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/force-confirm", null);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Owner_ForceConfirm_AlreadyDisputed_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = await CreatePendingMatchAsync(circleId, ids);

        // disputa come partecipante
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);

        // poi owner tenta di forzare
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/force-confirm", null);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task CrossCircle_OwnerA_ForceMatchInCircleB_Returns403()
    {
        // owner A crea il proprio circolo e una partita
        var (circleA, idsA, tokensA) = await SetupAsync();
        SetAuth(tokensA[0]);
        var matchInA = await CreatePendingMatchAsync(circleA, idsA);

        // owner B crea un secondo circolo separato
        var ownerBToken = await RegisterTokenAsync();
        SetAuth(ownerBToken);
        var circleBResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport = "padel" });
        var circleB = Guid.Parse((await circleBResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        // owner B tenta di forzare una partita del circolo A via URL del circolo B
        SetAuth(ownerBToken);
        var r = await _client.PostAsync($"/circles/{circleB}/matches/{matchInA}/force-confirm", null);

        // 404 perché la partita non appartiene al circolo B (multi-tenancy)
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static Guid GetId(JsonElement el) =>
        Guid.Parse(el.GetProperty("id").GetString()!);

    private async Task<Guid> CreatePendingMatchAsync(Guid circleId, Guid[] ids)
    {
        var r = await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { ids[0], ids[1] },
            team2 = new[] { ids[2], ids[3] },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });
        return GetId(await r.Content.ReadFromJsonAsync<JsonElement>());
    }

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupAsync()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse((await circleResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

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

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class ForceConfirmTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"ForceConfirmTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.AddSingleton<IRatingService>(RatingService);
        });

        builder.UseEnvironment("Testing");
    }
}
