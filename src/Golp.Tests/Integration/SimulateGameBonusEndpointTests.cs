using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Golp.Tests.Integration;

public class SimulateGameBonusEndpointTests : IClassFixture<SimulateTestFactory>
{
    private readonly HttpClient _client;

    public SimulateGameBonusEndpointTests(SimulateTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Calcolo base ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Simulate_BasePoints_NoBonus_EqualCurrentScores()
    {
        var body = new
        {
            sets = new[] { new { team1Score = 6, team2Score = 4 } },
            team1CurrentScore = 50,
            team2CurrentScore = 50
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, json.GetProperty("team1Points").GetInt32()); // 1 set vinto + diff 2
        Assert.Equal(0, json.GetProperty("team2Points").GetInt32());
    }

    [Fact]
    public async Task Simulate_MultiSet_AllSetsWon_SumsGameDiffOfWonSets()
    {
        var body = new
        {
            sets = new[]
            {
                new { team1Score = 6, team2Score = 2 },
                new { team1Score = 6, team2Score = 2 }
            },
            team1CurrentScore = 0,
            team2CurrentScore = 0
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(10, json.GetProperty("team1Points").GetInt32()); // 2 set vinti + gameDiff 4+4=8
    }

    [Fact]
    public async Task Simulate_WinnerLosesGameTotalButWinsOnSets_StaysPositive()
    {
        // Replica il bug segnalato: 6-4, 6-4, 1-6 → team1 vince 2 set su 3, ma game totali 13 vs 16.
        var body = new
        {
            sets = new[]
            {
                new { team1Score = 6, team2Score = 4 },
                new { team1Score = 6, team2Score = 4 },
                new { team1Score = 1, team2Score = 6 }
            },
            team1CurrentScore = 0,
            team2CurrentScore = 0
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(5, json.GetProperty("team1Points").GetInt32()); // setsWon 2 - setsLost 1 + gameDiff 4 = 5
        Assert.Equal(0, json.GetProperty("team2Points").GetInt32());
    }

    // ── Bonus upset ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Simulate_UnderdogWins_BonusApplied()
    {
        var body = new
        {
            sets = new[] { new { team1Score = 6, team2Score = 4 } },
            team1CurrentScore = 0,
            team2CurrentScore = 100
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(13, json.GetProperty("team1Points").GetInt32()); // base 3 + ceil(0.10*100)=10
    }

    [Fact]
    public async Task Simulate_FavoriteWins_NoBonus()
    {
        var body = new
        {
            sets = new[] { new { team1Score = 6, team2Score = 4 } },
            team1CurrentScore = 100,
            team2CurrentScore = 0
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(3, json.GetProperty("team1Points").GetInt32());
    }

    [Fact]
    public async Task Simulate_Team2Wins_PointsAssignedToTeam2()
    {
        var body = new
        {
            sets = new[] { new { team1Score = 4, team2Score = 6 } },
            team1CurrentScore = 0,
            team2CurrentScore = 0
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, json.GetProperty("team1Points").GetInt32());
        Assert.Equal(3, json.GetProperty("team2Points").GetInt32());
    }

    [Fact]
    public async Task Simulate_WinnerDeterminedBySetsWon_NotByTotalGames()
    {
        // team2 ha più game totali (16 vs 13) ma team1 vince 2 set su 3: deve vincere team1.
        var body = new
        {
            sets = new[]
            {
                new { team1Score = 6, team2Score = 4 },
                new { team1Score = 6, team2Score = 4 },
                new { team1Score = 1, team2Score = 6 }
            },
            team1CurrentScore = 0,
            team2CurrentScore = 0
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(json.GetProperty("team1Points").GetInt32() > 0);
        Assert.Equal(0, json.GetProperty("team2Points").GetInt32());
    }

    // ── Validazione ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Simulate_AllZeroScores_Returns400()
    {
        var body = new
        {
            sets = new[] { new { team1Score = 0, team2Score = 0 } }
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Simulate_EmptySets_Returns400()
    {
        var body = new
        {
            sets = Array.Empty<object>()
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Simulate_NegativeScore_Returns400()
    {
        var body = new
        {
            sets = new[] { new { team1Score = -1, team2Score = 4 } }
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Simulate_NegativeCurrentScore_Returns400()
    {
        var body = new
        {
            sets = new[] { new { team1Score = 6, team2Score = 4 } },
            team1CurrentScore = -5
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Simulate_SetsWonTied_Returns400()
    {
        var body = new
        {
            sets = new[]
            {
                new { team1Score = 6, team2Score = 4 },
                new { team1Score = 4, team2Score = 6 }
            }
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ── Endpoint pubblico, no auth ───────────────────────────────────────────

    [Fact]
    public async Task Simulate_NoAuthHeader_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var body = new
        {
            sets = new[] { new { team1Score = 6, team2Score = 4 } }
        };

        var resp = await _client.PostAsJsonAsync("/api/simulate-game-bonus", body);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
