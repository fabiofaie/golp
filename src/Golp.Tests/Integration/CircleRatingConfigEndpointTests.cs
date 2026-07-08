using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Golp.Tests.Integration;

/// <summary>
/// US-051: PUT /circles/{id}/rating-config — solo owner cambia il metodo di calcolo punteggio
/// (ELO / Game+Bonus) e i parametri di finestra, senza ricalcolare lo storico.
/// </summary>
public class CircleRatingConfigEndpointTests : IClassFixture<JoinCircleTestFactory>
{
    private readonly JoinCircleTestFactory _factory;
    private readonly HttpClient _client;

    public CircleRatingConfigEndpointTests(JoinCircleTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_UpdateToGameBonus_Returns200AndPersisted()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var response = await _client.PutAsJsonAsync($"/circles/{circleId}/rating-config",
            new { ratingMethod = "GameBonus", gameBonusWindowMatches = 20, gameBonusWindowWeeks = 4 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("GameBonus", body.GetProperty("ratingMethod").GetString());
        Assert.Equal(20, body.GetProperty("gameBonusWindowMatches").GetInt32());
        Assert.Equal(4, body.GetProperty("gameBonusWindowWeeks").GetInt32());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var circle = await db.Circles.FindAsync(circleId);
        Assert.Equal("GameBonus", circle!.RatingMethod);
        Assert.Equal(20, circle.GameBonusWindowMatches);
        Assert.Equal(4, circle.GameBonusWindowWeeks);
    }

    [Fact]
    public async Task NonOwner_Update_Returns403()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var otherToken = await RegisterAndGetTokenAsync();
        SetAuth(otherToken);

        var response = await _client.PutAsJsonAsync($"/circles/{circleId}/rating-config",
            new { ratingMethod = "GameBonus" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InvalidRatingMethod_Returns400()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var response = await _client.PutAsJsonAsync($"/circles/{circleId}/rating-config",
            new { ratingMethod = "Bogus" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0, 6)]     // matches sotto range
    [InlineData(201, 6)]   // matches sopra range
    [InlineData(30, 0)]    // weeks sotto range
    [InlineData(30, 53)]   // weeks sopra range
    public async Task WindowParamsOutOfRange_Returns400(int matches, int weeks)
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var response = await _client.PutAsJsonAsync($"/circles/{circleId}/rating-config",
            new { ratingMethod = "GameBonus", gameBonusWindowMatches = matches, gameBonusWindowWeeks = weeks });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Owner_TogglesBackAndForth_NeverBlocked()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var r1 = await _client.PutAsJsonAsync($"/circles/{circleId}/rating-config", new { ratingMethod = "GameBonus" });
        var r2 = await _client.PutAsJsonAsync($"/circles/{circleId}/rating-config", new { ratingMethod = "Elo" });
        var r3 = await _client.PutAsJsonAsync($"/circles/{circleId}/rating-config", new { ratingMethod = "GameBonus" });

        Assert.Equal(HttpStatusCode.OK, r1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        Assert.Equal(HttpStatusCode.OK, r3.StatusCode);
    }

    [Fact]
    public async Task CircleNotFound_Returns404()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var response = await _client.PutAsJsonAsync($"/circles/{Guid.NewGuid()}/rating-config",
            new { ratingMethod = "GameBonus" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // GET /circles/me riflette subito il metodo aggiornato, senza toccare lo storico
    [Fact]
    public async Task GetMyCircles_ReflectsUpdatedConfig_Immediately()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        await _client.PutAsJsonAsync($"/circles/{circleId}/rating-config",
            new { ratingMethod = "GameBonus", gameBonusWindowMatches = 15, gameBonusWindowWeeks = 3 });

        var body = await _client.GetFromJsonAsync<JsonElement>("/circles/me");
        var mine = body.EnumerateArray().First(c => c.GetProperty("id").GetString() == circleId.ToString());

        Assert.Equal("GameBonus", mine.GetProperty("ratingMethod").GetString());
        Assert.Equal(15, mine.GetProperty("gameBonusWindowMatches").GetInt32());
        Assert.Equal(3, mine.GetProperty("gameBonusWindowWeeks").GetInt32());
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> CreatePublicCircleAsync()
    {
        var response = await _client.PostAsJsonAsync("/circles",
            new { name = $"RC_{Guid.NewGuid():N}", sport = "padel" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"rc_{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
