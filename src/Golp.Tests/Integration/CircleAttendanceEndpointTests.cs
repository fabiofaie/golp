using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Golp.Tests.Integration;

/// <summary>
/// US-049: POST /circles/{id}/attendance — check-in per il raduno. Round-trip dell'entità
/// CircleAttendance e regole di autorizzazione (self sempre, altrui solo owner).
/// </summary>
public class CircleAttendanceEndpointTests : IClassFixture<JoinCircleTestFactory>
{
    private readonly JoinCircleTestFactory _factory;
    private readonly HttpClient _client;

    public CircleAttendanceEndpointTests(JoinCircleTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SelfCheckin_Returns200AndPersists()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var response = await _client.PostAsJsonAsync($"/circles/{circleId}/attendance", new { present = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await db.CircleAttendances.AnyAsync(a => a.CircleId == circleId));
    }

    [Fact]
    public async Task SelfCheckout_RemovesAttendance()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        await _client.PostAsJsonAsync($"/circles/{circleId}/attendance", new { present = true });
        var response = await _client.PostAsJsonAsync($"/circles/{circleId}/attendance", new { present = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.CircleAttendances.AnyAsync(a => a.CircleId == circleId));
    }

    [Fact]
    public async Task Owner_CheckinOnBehalfOfMember_Returns200()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var memberToken = await RegisterAndGetTokenAsync();
        var memberId = await JoinAsAsync(memberToken, circleId);

        SetAuth(ownerToken);
        var response = await _client.PostAsJsonAsync($"/circles/{circleId}/attendance", new { present = true, userId = memberId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task NonOwner_CheckinOnBehalfOfAnother_Returns403()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var memberAToken = await RegisterAndGetTokenAsync();
        await JoinAsAsync(memberAToken, circleId);
        var memberBToken = await RegisterAndGetTokenAsync();
        var memberBId = await JoinAsAsync(memberBToken, circleId);

        SetAuth(memberAToken);
        var response = await _client.PostAsJsonAsync($"/circles/{circleId}/attendance", new { present = true, userId = memberBId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task NonMember_Checkin_Returns400()
    {
        var ownerToken = await RegisterAndGetTokenAsync();
        SetAuth(ownerToken);
        var circleId = await CreatePublicCircleAsync();

        var outsiderToken = await RegisterAndGetTokenAsync();
        SetAuth(outsiderToken);

        var response = await _client.PostAsJsonAsync($"/circles/{circleId}/attendance", new { present = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CircleNotFound_Returns404()
    {
        var token = await RegisterAndGetTokenAsync();
        SetAuth(token);

        var response = await _client.PostAsJsonAsync($"/circles/{Guid.NewGuid()}/attendance", new { present = true });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> CreatePublicCircleAsync()
    {
        var response = await _client.PostAsJsonAsync("/circles",
            new { name = $"AT_{Guid.NewGuid():N}", sport = "padel" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid> JoinAsAsync(string token, Guid circleId)
    {
        SetAuth(token);
        await _client.PostAsync($"/circles/{circleId}/join", null);
        var meResponse = await _client.GetAsync("/auth/me");
        var body = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<string> RegisterAndGetTokenAsync()
    {
        var email = $"at_{Guid.NewGuid():N}@test.com";
        var response = await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
