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

/// <summary>
/// US-052: alla conferma, il metodo di calcolo attivo del circolo (Circle.RatingMethod) decide
/// se gira ELO o Game+Bonus. Circle.RatingMethod è impostato direttamente in DB in questi test
/// perché l'endpoint di configurazione (US-051) non è ancora implementato.
/// </summary>
public class GameBonusConfirmTests : IClassFixture<GameBonusConfirmTestFactory>
{
    private readonly HttpClient _client;
    private readonly GameBonusConfirmTestFactory _factory;

    public GameBonusConfirmTests(GameBonusConfirmTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Circle_GameBonus_Confirm4th_PersistsPointsAndSkipsElo()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        await SetRatingMethodAsync(circleId, "GameBonus");

        var matchId = await CreateAndFullyConfirmAsync(circleId, ids, tokens);

        Assert.False(_factory.RatingService.WasCalledWith(matchId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = await db.Matches.FindAsync(matchId);

        Assert.NotNull(match!.GameBonusWinnerPoints);
        Assert.Equal(3, match.GameBonusWinnerPoints); // 6-4 → (6-4)+1 = 3, nessuna storia pregressa
        // I delta sono valorizzati anche per Game+Bonus (espongono il punteggio in UI, vedi
        // GameBonusRatingService.CalculateAndApplyAsync), non sono più esclusivi dell'ELO.
        Assert.Equal(3, match.DeltaTeam1Player1);
    }

    [Fact]
    public async Task Circle_Elo_Confirm4th_CallsEloAndSkipsGameBonus()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        // RatingMethod di default resta "Elo" — nessuna modifica

        var matchId = await CreateAndFullyConfirmAsync(circleId, ids, tokens);

        Assert.True(_factory.RatingService.WasCalledWith(matchId));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = await db.Matches.FindAsync(matchId);

        Assert.Null(match!.GameBonusWinnerPoints);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task SetRatingMethodAsync(Guid circleId, string method)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var circle = await db.Circles.FindAsync(circleId);
        circle!.RatingMethod = method;
        await db.SaveChangesAsync();
    }

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

// ─── Test Rating Service (solo per ELO — Game+Bonus usa l'implementazione reale) ─────────────

public class GameBonusTestEloRatingService : IRatingService
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

public class GameBonusConfirmTestFactory : WebApplicationFactory<Program>
{
    public GameBonusTestEloRatingService RatingService { get; } = new();

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
            var dbName = $"GameBonusConfirmTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.AddSingleton<IRatingService>(RatingService);
            // IGameBonusRatingService NON sostituito: usa l'implementazione reale registrata da Program.cs
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
