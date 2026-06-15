using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Golp.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Golp.Tests.Integration;

public class SimulateEndpointTests : IClassFixture<SimulateTestFactory>
{
    private readonly HttpClient _client;

    public SimulateEndpointTests(SimulateTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── AC4: calcolo identico al backend ────────────────────────────────────

    [Fact]
    public async Task SimulateMatch_EqualRatings_PositiveDeltaForWinner()
    {
        var body = new
        {
            team1 = new { player1Rating = 1000, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = new[] { new { team1Score = 10, team2Score = 6 } },
            experienced = true
        };

        var resp = await _client.PostAsJsonAsync("/simulate-match", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        int d1p1 = json.GetProperty("deltaTeam1Player1").GetInt32();
        int d1p2 = json.GetProperty("deltaTeam1Player2").GetInt32();
        int d2p1 = json.GetProperty("deltaTeam2Player1").GetInt32();
        int d2p2 = json.GetProperty("deltaTeam2Player2").GetInt32();

        Assert.True(d1p1 > 0, "vincitore deve guadagnare punti");
        Assert.True(d1p2 > 0);
        Assert.True(d2p1 < 0, "perdente deve perdere punti");
        Assert.True(d2p2 < 0);
        Assert.Equal(d1p1, -d2p1); // simmetrico per rating pari
        Assert.Equal(d1p2, -d2p2);
    }

    [Fact]
    public async Task SimulateMatch_UnderdogWins_DeltaHigherThanEqualCase()
    {
        // underdog (800) batte favorito (1200) — delta vincitore maggiore del caso pari
        var underdogBody = new
        {
            team1 = new { player1Rating = 800, player2Rating = 800 },
            team2 = new { player1Rating = 1200, player2Rating = 1200 },
            sets  = new[] { new { team1Score = 10, team2Score = 6 } },
            experienced = true
        };

        var equalBody = new
        {
            team1 = new { player1Rating = 1000, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = new[] { new { team1Score = 10, team2Score = 6 } },
            experienced = true
        };

        var underdogResp = await _client.PostAsJsonAsync("/simulate-match", underdogBody);
        var equalResp    = await _client.PostAsJsonAsync("/simulate-match", equalBody);

        var uj = await underdogResp.Content.ReadFromJsonAsync<JsonElement>();
        var ej = await equalResp.Content.ReadFromJsonAsync<JsonElement>();

        int underdogDelta = uj.GetProperty("deltaTeam1Player1").GetInt32();
        int equalDelta    = ej.GetProperty("deltaTeam1Player1").GetInt32();

        Assert.True(underdogDelta > equalDelta, "underdog che vince guadagna più dell'esito paritario");
    }

    [Fact]
    public async Task SimulateMatch_FavoriteWins_DeltaLowerThanEqualCase()
    {
        var favoriteBody = new
        {
            team1 = new { player1Rating = 1200, player2Rating = 1200 },
            team2 = new { player1Rating = 800, player2Rating = 800 },
            sets  = new[] { new { team1Score = 10, team2Score = 6 } },
            experienced = true
        };

        var equalBody = new
        {
            team1 = new { player1Rating = 1000, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = new[] { new { team1Score = 10, team2Score = 6 } },
            experienced = true
        };

        var favResp   = await _client.PostAsJsonAsync("/simulate-match", favoriteBody);
        var equalResp = await _client.PostAsJsonAsync("/simulate-match", equalBody);

        var fj = await favResp.Content.ReadFromJsonAsync<JsonElement>();
        var ej = await equalResp.Content.ReadFromJsonAsync<JsonElement>();

        int favDelta   = fj.GetProperty("deltaTeam1Player1").GetInt32();
        int equalDelta = ej.GetProperty("deltaTeam1Player1").GetInt32();

        Assert.True(favDelta < equalDelta, "favorito che vince guadagna meno dell'esito paritario");
    }

    [Fact]
    public async Task SimulateMatch_NewPlayer_HigherDeltaThanExperienced()
    {
        // K=48 (nuovo) deve dare delta maggiore di K=32 (esperto), stesso scenario
        var newPlayerBody = new
        {
            team1 = new { player1Rating = 1000, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = new[] { new { team1Score = 10, team2Score = 6 } },
            experienced = false
        };

        var expBody = new
        {
            team1 = new { player1Rating = 1000, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = new[] { new { team1Score = 10, team2Score = 6 } },
            experienced = true
        };

        var newResp = await _client.PostAsJsonAsync("/simulate-match", newPlayerBody);
        var expResp = await _client.PostAsJsonAsync("/simulate-match", expBody);

        var nj = await newResp.Content.ReadFromJsonAsync<JsonElement>();
        var ej = await expResp.Content.ReadFromJsonAsync<JsonElement>();

        int newDelta = nj.GetProperty("deltaTeam1Player1").GetInt32();
        int expDelta = ej.GetProperty("deltaTeam1Player1").GetInt32();

        Assert.True(newDelta > expDelta, "nuovo giocatore (K=48) deve avere delta maggiore di esperto (K=32)");
    }

    [Fact]
    public async Task SimulateMatch_MultiSet_ScoreRatioFromTotalPoints()
    {
        // 6-4, 7-5 → totale 13 vs 9 → team1 vince
        var body = new
        {
            team1 = new { player1Rating = 1000, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = new[]
            {
                new { team1Score = 6, team2Score = 4 },
                new { team1Score = 7, team2Score = 5 }
            },
            experienced = true
        };

        var resp = await _client.PostAsJsonAsync("/simulate-match", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.GetProperty("deltaTeam1Player1").GetInt32() > 0);
    }

    // ── AC5: validazione ─────────────────────────────────────────────────────

    [Fact]
    public async Task SimulateMatch_AllZeroScores_Returns400()
    {
        var body = new
        {
            team1 = new { player1Rating = 1000, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = new[] { new { team1Score = 0, team2Score = 0 } },
            experienced = true
        };

        var resp = await _client.PostAsJsonAsync("/simulate-match", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task SimulateMatch_RatingOutOfRange_Returns400()
    {
        var body = new
        {
            team1 = new { player1Rating = 3001, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = new[] { new { team1Score = 10, team2Score = 6 } },
            experienced = true
        };

        var resp = await _client.PostAsJsonAsync("/simulate-match", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task SimulateMatch_EmptySets_Returns400()
    {
        var body = new
        {
            team1 = new { player1Rating = 1000, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = Array.Empty<object>(),
            experienced = true
        };

        var resp = await _client.PostAsJsonAsync("/simulate-match", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── AC1: endpoint pubblico, no auth ──────────────────────────────────────

    [Fact]
    public async Task SimulateMatch_NoAuthHeader_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var body = new
        {
            team1 = new { player1Rating = 1000, player2Rating = 1000 },
            team2 = new { player1Rating = 1000, player2Rating = 1000 },
            sets  = new[] { new { team1Score = 10, team2Score = 6 } },
            experienced = true
        };

        var resp = await _client.PostAsJsonAsync("/simulate-match", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ── Verifica valori numerici tramite ComputeDeltas direttamente ──────────

    [Fact]
    public void ComputeDeltas_EqualRatings_SymmetricResult()
    {
        // score_ratio per 10-6: 10/16 = 0.625
        double scoreRatio = Math.Clamp(10.0 / 16.0, 0.5, 1.0);
        var deltas = RatingService.ComputeDeltas(1000, 1000, [32, 32, 32, 32], true, scoreRatio);

        Assert.Equal(deltas[0], deltas[1]);
        Assert.Equal(deltas[2], deltas[3]);
        Assert.Equal(deltas[0], -deltas[2]);
        Assert.True(deltas[0] > 0);
    }
}

public class SimulateTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"SimulateTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));
        });

        builder.UseEnvironment("Testing");
    }
}
