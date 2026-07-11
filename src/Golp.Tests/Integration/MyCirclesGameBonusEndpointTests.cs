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
/// US-073: GET /circles/me deve riportare rating/posizione coerenti col metodo di calcolo del
/// circolo (ELO vs Game+Bonus), non sempre il rating ELO — stesso criterio di GetCircleLeaderboardAsync
/// (US-052). Prima del fix, myRating/myRank venivano sempre calcolati sul campo ELO.
/// </summary>
public class MyCirclesGameBonusEndpointTests : IClassFixture<MyCirclesGameBonusTestFactory>
{
    private readonly HttpClient _client;
    private readonly MyCirclesGameBonusTestFactory _factory;

    public MyCirclesGameBonusEndpointTests(MyCirclesGameBonusTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MyCircles_GameBonus_MatchesLeaderboardRankAndScore()
    {
        var (circleId, ids, ownerToken) = await SetupCircleAsync();

        // Due partite 2v2 complete (nessun singles, per non urtare il bug pre-esistente di
        // GetCircleLeaderboardAsync su match con slot vuoti) con punteggi accumulati diversi
        // per ogni giocatore, cosi il ranking non dipende da un tie-break indefinito:
        // ids[0]=5+9=14, ids[1]=5, ids[2]=9, ids[3]=0.
        await SeedConfirmedMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3], winnerTeam: 1, points: 5);
        await SeedConfirmedMatchAsync(circleId, ids[0], ids[2], ids[1], ids[3], winnerTeam: 1, points: 9);

        SetAuth(ownerToken);
        var myCircles = await _client.GetFromJsonAsync<JsonElement>("/circles/me");
        var leaderboard = await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/leaderboard");

        var myEntry = myCircles.EnumerateArray().First(c => c.GetProperty("id").GetString() == circleId.ToString());
        Assert.Equal("GameBonus", myEntry.GetProperty("ratingMethod").GetString());

        var leaderboardWinner = leaderboard.GetProperty("classified").EnumerateArray()
            .First(m => m.GetProperty("userId").GetString() == ids[0].ToString());

        Assert.Equal(leaderboardWinner.GetProperty("gameBonusPoints").GetInt32(), myEntry.GetProperty("myRating").GetInt32());
        Assert.Equal(leaderboardWinner.GetProperty("rank").GetInt32(), myEntry.GetProperty("myRank").GetInt32());
        Assert.Equal(1, myEntry.GetProperty("myRank").GetInt32()); // vincitore unico match, primo in classifica
    }

    [Fact]
    public async Task MyCircles_Elo_StillUsesRatingField()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"Elo_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse((await circleResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        var myCircles = await _client.GetFromJsonAsync<JsonElement>("/circles/me");
        var myEntry = myCircles.EnumerateArray().First(c => c.GetProperty("id").GetString() == circleId.ToString());

        Assert.Equal("Elo", myEntry.GetProperty("ratingMethod").GetString());
        Assert.Equal(1000, myEntry.GetProperty("myRating").GetInt32());
        Assert.Equal(1, myEntry.GetProperty("myRank").GetInt32());
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] Ids, string OwnerToken)> SetupCircleAsync()
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
        circle.GameBonusWindowMatches = 30;
        circle.GameBonusWindowWeeks = 6;
        await db.SaveChangesAsync();

        return (circleId, [ownerId, p2, p3, p4], ownerToken);
    }

    private async Task SeedConfirmedMatchAsync(
        Guid circleId, Guid w1, Guid w2, Guid l1, Guid l2, int winnerTeam, int points)
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
            CreatedAt             = DateTimeOffset.UtcNow.AddDays(-1),
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
        var email = $"mc_{Guid.NewGuid():N}@test.com";
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

public class MyCirclesGameBonusTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"MyCirclesGameBonusTestDb_{Guid.NewGuid()}";
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
