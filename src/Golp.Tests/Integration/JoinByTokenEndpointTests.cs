using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Golp.Tests.Integration;

public class JoinByTokenEndpointTests : IClassFixture<JoinByTokenTestFactory>
{
    private readonly HttpClient _client;

    public JoinByTokenEndpointTests(JoinByTokenTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ValidToken_NewUser_Returns200_AlreadyMemberFalse()
    {
        var (token, circleId) = await SetupCircleWithInviteAsync();
        var memberToken = await RegisterTokenAsync();
        SetAuth(memberToken);

        var r = await _client.PostAsJsonAsync("/circles/join-by-token", new { inviteToken = token });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(circleId.ToString(), body.GetProperty("circleId").GetString());
        Assert.Equal(1000, body.GetProperty("myRating").GetInt32());
        Assert.False(body.GetProperty("alreadyMember").GetBoolean());
    }

    [Fact]
    public async Task ValidToken_AlreadyMember_Returns200_AlreadyMemberTrue()
    {
        var (token, _) = await SetupCircleWithInviteAsync();
        var memberToken = await RegisterTokenAsync();
        SetAuth(memberToken);

        await _client.PostAsJsonAsync("/circles/join-by-token", new { inviteToken = token });
        var r2 = await _client.PostAsJsonAsync("/circles/join-by-token", new { inviteToken = token });

        Assert.Equal(HttpStatusCode.OK, r2.StatusCode);
        var body = await r2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("alreadyMember").GetBoolean());
    }

    [Fact]
    public async Task InvalidToken_Returns404()
    {
        var memberToken = await RegisterTokenAsync();
        SetAuth(memberToken);

        var r = await _client.PostAsJsonAsync("/circles/join-by-token", new { inviteToken = "nonexistenttoken" });

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var r = await _client.PostAsJsonAsync("/circles/join-by-token", new { inviteToken = "anytoken" });

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task NewMember_RatingIs1000()
    {
        var (token, circleId) = await SetupCircleWithInviteAsync();
        var memberToken = await RegisterTokenAsync();
        SetAuth(memberToken);

        var r = await _client.PostAsJsonAsync("/circles/join-by-token", new { inviteToken = token });

        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1000, body.GetProperty("myRating").GetInt32());
    }

    [Fact]
    public async Task InviteInfo_ValidToken_Returns200WithCircleName()
    {
        var (token, _) = await SetupCircleWithInviteAsync();
        _client.DefaultRequestHeaders.Authorization = null;

        var r = await _client.GetAsync($"/circles/invite/{token}");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("valid").GetBoolean());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("circleName").GetString()));
    }

    [Fact]
    public async Task InviteInfo_InvalidToken_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization = null;

        var r = await _client.GetAsync("/circles/invite/nonexistenttoken");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("valid").GetBoolean());
    }

    // ─── helpers ────────────────────────────────────────────────────────────────

    private async Task<(string InviteToken, Guid CircleId)> SetupCircleWithInviteAsync()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"JBT_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse(
            (await circleResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        var linkResp = await _client.GetAsync($"/circles/{circleId}/invite-link");
        var inviteToken = (await linkResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("inviteToken").GetString()!;

        return (inviteToken, circleId);
    }

    private async Task<string> RegisterTokenAsync()
    {
        var email = $"jbt_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Player", email, password = "password123" });
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class JoinByTokenTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"JoinByTokenTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));
        });

        builder.UseEnvironment("Testing");
    }
}
