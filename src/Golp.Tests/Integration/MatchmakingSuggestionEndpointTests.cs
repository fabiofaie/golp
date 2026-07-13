using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Golp.Tests.Integration;

/// <summary>
/// US-049: POST /circles/{id}/matchmaking-suggestion — piano multi-turno dai presenti correnti.
/// </summary>
public class MatchmakingSuggestionEndpointTests : IClassFixture<JoinCircleTestFactory>
{
    private readonly JoinCircleTestFactory _factory;
    private readonly HttpClient _client;

    public MatchmakingSuggestionEndpointTests(JoinCircleTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FourPresent_OneCourt_ReturnsSingleRoundWithOneMatch()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();
        await CheckInAsync(circleId);

        for (var i = 0; i < 3; i++)
        {
            var token = await RegisterAndGetTokenAsync();
            await JoinAsync(token, circleId);
            await CheckInAsync(circleId);
        }

        SetAuth(ownerToken);
        var response = await _client.PostAsJsonAsync($"/circles/{circleId}/matchmaking-suggestion",
            new { courts = 1, targetMode = "Total", targetValue = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var rounds = body.GetProperty("rounds");
        Assert.Equal(1, rounds.GetArrayLength());
        Assert.Equal(1, rounds[0].GetProperty("matches").GetArrayLength());
    }

    [Fact]
    public async Task FewerThanFourPresent_Returns400()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();
        await CheckInAsync(circleId);

        var response = await _client.PostAsJsonAsync($"/circles/{circleId}/matchmaking-suggestion",
            new { courts = 1, targetMode = "Total", targetValue = 1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(0, "Total", 1)]
    [InlineData(1, "Total", 0)]
    [InlineData(1, "Bogus", 1)]
    public async Task InvalidParams_Returns400(int courts, string targetMode, int targetValue)
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var response = await _client.PostAsJsonAsync($"/circles/{circleId}/matchmaking-suggestion",
            new { courts, targetMode, targetValue });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DifferentCircle_DoesNotSeeAttendanceFromAnotherCircle()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleA = await CreatePublicCircleAsync();
        var circleB = await CreatePublicCircleAsync();

        // 4 presenti solo nel circolo A
        await _client.PostAsJsonAsync($"/circles/{circleA}/attendance", new { present = true });
        for (var i = 0; i < 3; i++)
        {
            var token = await RegisterAndGetTokenAsync();
            await JoinAsync(token, circleA);
            SetAuth(token);
            await _client.PostAsJsonAsync($"/circles/{circleA}/attendance", new { present = true });
        }

        SetAuth(ownerToken);
        var response = await _client.PostAsJsonAsync($"/circles/{circleB}/matchmaking-suggestion",
            new { courts = 1, targetMode = "Total", targetValue = 1 });

        // owner non ha fatto check-in nel circolo B, quindi < 4 presenti anche se è owner di entrambi
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> CreatePublicCircleAsync()
    {
        var response = await _client.PostAsJsonAsync("/circles",
            new { name = $"MM_{Guid.NewGuid():N}", sport = "padel" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task JoinAsync(string token, Guid circleId)
    {
        SetAuth(token);
        await _client.PostAsync($"/circles/{circleId}/join", null);
    }

    private async Task CheckInAsync(Guid circleId) =>
        await _client.PostAsJsonAsync($"/circles/{circleId}/attendance", new { present = true });

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"mm_{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
