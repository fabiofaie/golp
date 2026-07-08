using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Golp.Tests.Integration;

/// <summary>
/// US-052: GET /circles/{id}/leaderboard, per circoli con RatingMethod = "GameBonus", somma i punti
/// solo sulle partite che rientrano nella finestra (ultime N ∩ ultime M settimane) del circolo.
/// I match sono seminati direttamente in DB (non via HTTP) per controllare CreatedAt e simulare storico.
/// </summary>
public class GameBonusLeaderboardEndpointTests : IClassFixture<GameBonusLeaderboardTestFactory>
{
    private readonly HttpClient _client;
    private readonly GameBonusLeaderboardTestFactory _factory;

    public GameBonusLeaderboardEndpointTests(GameBonusLeaderboardTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Leaderboard_GameBonus_OnlyCountsMatchesWithinWeeksWindow()
    {
        var (circleId, ids, ownerToken) = await SetupCircleAsync(windowMatches: 30, windowWeeks: 6);

        await SeedConfirmedMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3], winnerTeam: 1, points: 5, createdAt: DateTimeOffset.UtcNow.AddDays(-1));
        await SeedConfirmedMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3], winnerTeam: 1, points: 7, createdAt: DateTimeOffset.UtcNow.AddDays(-50)); // fuori finestra (>6 settimane)

        SetAuth(ownerToken);
        var body = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/leaderboard");

        Assert.Equal("GameBonus", body.GetProperty("ratingMethod").GetString());
        var winner = body.GetProperty("classified").EnumerateArray().First(m => m.GetProperty("userId").GetString() == ids[0].ToString());
        Assert.Equal(5, winner.GetProperty("gameBonusPoints").GetInt32()); // solo la partita recente conta
    }

    [Fact]
    public async Task Leaderboard_GameBonus_OnlyCountsLatestNMatches()
    {
        var (circleId, ids, ownerToken) = await SetupCircleAsync(windowMatches: 1, windowWeeks: 52);

        await SeedConfirmedMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3], winnerTeam: 1, points: 5, createdAt: DateTimeOffset.UtcNow.AddDays(-3));
        await SeedConfirmedMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3], winnerTeam: 1, points: 9, createdAt: DateTimeOffset.UtcNow.AddDays(-1));

        SetAuth(ownerToken);
        var body = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/leaderboard");

        var winner = body.GetProperty("classified").EnumerateArray().First(m => m.GetProperty("userId").GetString() == ids[0].ToString());
        Assert.Equal(9, winner.GetProperty("gameBonusPoints").GetInt32()); // solo la più recente delle N=1
    }

    [Fact]
    public async Task Leaderboard_ChangingWindowParams_RecalculatesWithoutTouchingStoredPoints()
    {
        var (circleId, ids, ownerToken) = await SetupCircleAsync(windowMatches: 1, windowWeeks: 52);

        await SeedConfirmedMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3], winnerTeam: 1, points: 5, createdAt: DateTimeOffset.UtcNow.AddDays(-3));
        await SeedConfirmedMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3], winnerTeam: 1, points: 9, createdAt: DateTimeOffset.UtcNow.AddDays(-1));

        SetAuth(ownerToken);
        var before = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/leaderboard");
        var winnerBefore = before.GetProperty("classified").EnumerateArray().First(m => m.GetProperty("userId").GetString() == ids[0].ToString());
        Assert.Equal(9, winnerBefore.GetProperty("gameBonusPoints").GetInt32());

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var circle = await db.Circles.FindAsync(circleId);
            circle!.GameBonusWindowMatches = 2;
            await db.SaveChangesAsync();
        }

        var after = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/leaderboard");
        var winnerAfter = after.GetProperty("classified").EnumerateArray().First(m => m.GetProperty("userId").GetString() == ids[0].ToString());
        Assert.Equal(14, winnerAfter.GetProperty("gameBonusPoints").GetInt32()); // ora entrambe: 5+9

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storedPoints = await verifyDb.Matches
            .Where(m => m.CircleId == circleId)
            .Select(m => m.GameBonusWinnerPoints!.Value)
            .ToListAsync();
        Assert.Equal(new[] { 5, 9 }, storedPoints.OrderBy(p => p).ToArray());
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] Ids, string OwnerToken)> SetupCircleAsync(int windowMatches, int windowWeeks)
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"GB_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse((await circleResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        var ownerId = ExtractUserIdFromJwt(ownerToken);
        var (p2, _) = await RegisterAndJoinAsync(circleId);
        var (p3, _) = await RegisterAndJoinAsync(circleId);
        var (p4, _) = await RegisterAndJoinAsync(circleId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var circle = await db.Circles.FindAsync(circleId);
        circle!.RatingMethod = "GameBonus";
        circle.GameBonusWindowMatches = windowMatches;
        circle.GameBonusWindowWeeks = windowWeeks;
        await db.SaveChangesAsync();

        return (circleId, [ownerId, p2, p3, p4], ownerToken);
    }

    private async Task SeedConfirmedMatchAsync(
        Guid circleId, Guid w1, Guid w2, Guid l1, Guid l2, int winnerTeam, int points, DateTimeOffset createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Matches.Add(new Match
        {
            CircleId              = circleId,
            CreatedById           = w1,
            Status                = "confirmed",
            WinnerTeam            = winnerTeam,
            Team1Player1Id        = w1,
            Team1Player2Id        = w2,
            Team2Player1Id        = l1,
            Team2Player2Id        = l2,
            GameBonusWinnerPoints = points,
            CreatedAt             = createdAt,
        });
        await db.SaveChangesAsync();
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
        var email = $"gb_{Guid.NewGuid():N}@test.com";
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

public class GameBonusLeaderboardTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"GameBonusLeaderboardTestDb_{Guid.NewGuid()}";
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
