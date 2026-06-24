using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Golp.Tests.Integration;

public class InviteLinkEndpointTests : IClassFixture<InviteLinkTestFactory>
{
    private readonly HttpClient _client;

    public InviteLinkEndpointTests(InviteLinkTestFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Owner_Gets200_WithInviteToken()
    {
        var (circleId, ownerToken, _) = await SetupCircleWithMemberAsync();
        SetAuth(ownerToken);

        var r = await _client.GetAsync($"/circles/{circleId}/invite-link");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("inviteToken").GetString();
        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task SecondCall_ReturnsSameToken()
    {
        var (circleId, ownerToken, _) = await SetupCircleWithMemberAsync();
        SetAuth(ownerToken);

        var r1 = await _client.GetAsync($"/circles/{circleId}/invite-link");
        var r2 = await _client.GetAsync($"/circles/{circleId}/invite-link");

        var t1 = (await r1.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("inviteToken").GetString();
        var t2 = (await r2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("inviteToken").GetString();
        Assert.Equal(t1, t2);
    }

    [Fact]
    public async Task NonOwner_Gets403()
    {
        var (circleId, _, memberToken) = await SetupCircleWithMemberAsync();
        SetAuth(memberToken);

        var r = await _client.GetAsync($"/circles/{circleId}/invite-link");

        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_Gets401()
    {
        var (circleId, _, _) = await SetupCircleWithMemberAsync();
        _client.DefaultRequestHeaders.Authorization = null;

        var r = await _client.GetAsync($"/circles/{circleId}/invite-link");

        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task NotFound_Gets404()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);

        var r = await _client.GetAsync($"/circles/{Guid.NewGuid()}/invite-link");

        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }

    // ─── helpers ────────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, string OwnerToken, string MemberToken)> SetupCircleWithMemberAsync()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"IL_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse(
            (await circleResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        var memberToken = await RegisterTokenAsync();
        SetAuth(memberToken);
        await _client.PostAsync($"/circles/{circleId}/join", null);

        return (circleId, ownerToken, memberToken);
    }

    private async Task<string> RegisterTokenAsync()
    {
        var email = $"il_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Player", email, password = "password123" });
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class InviteLinkTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"InviteLinkTestDb_{Guid.NewGuid()}";
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
