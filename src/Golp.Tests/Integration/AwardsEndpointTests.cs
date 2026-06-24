using System.Net;
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

public class AwardsEndpointTests : IClassFixture<AwardsTestFactory>
{
    private readonly HttpClient _client;
    private readonly AwardsTestFactory _factory;

    // Dates used across tests — computed once so they're stable regardless of clock during run
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static readonly DateTimeOffset ThisMonth = new(Now.Year, Now.Month, 1, 0, 0, 0, TimeSpan.Zero);
    // A month inside the current year but definitely != current month (use Jan or Jun)
    private static readonly DateTimeOffset OtherMonthThisYear = Now.Month != 1
        ? new DateTimeOffset(Now.Year, 1, 1, 0, 0, 0, TimeSpan.Zero)
        : new DateTimeOffset(Now.Year, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LastYear = new(Now.Year - 1, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public AwardsEndpointTests(AwardsTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Winner = player con net gain più alto nel mese corrente
    [Fact]
    public async Task GetAwards_CurrentMonth_ReturnsWinnerWithHighestNetGain()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        await SeedMatchAsync(circleId, ids, ThisMonth, deltaT1P1: 30, deltaT1P2: 20, deltaT2P1: -10, deltaT2P2: -10);

        var body = await GetAwardsAsync(circleId, tokens[0]);
        var winner = body.GetProperty("currentMonth").GetProperty("winner");

        Assert.NotEqual(JsonValueKind.Null, winner.ValueKind);
        Assert.Equal(ids[0].ToString(), winner.GetProperty("userId").GetString());
        Assert.Equal(30, winner.GetProperty("netGain").GetInt32());
        Assert.Equal(1, winner.GetProperty("matchesPlayed").GetInt32());
    }

    // Partite dell'anno scorso non entrano nel mese corrente
    [Fact]
    public async Task GetAwards_LastYearMatches_NotCountedInCurrentMonth()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        await SeedMatchAsync(circleId, ids, LastYear, deltaT1P1: 50, deltaT1P2: 50, deltaT2P1: -50, deltaT2P2: -50);

        var body = await GetAwardsAsync(circleId, tokens[0]);
        var currentMonthWinner = body.GetProperty("currentMonth").GetProperty("winner");

        Assert.Equal(JsonValueKind.Null, currentMonthWinner.ValueKind);
    }

    // Circolo senza partite confirmed → winner null per entrambi i periodi
    [Fact]
    public async Task GetAwards_NoConfirmedMatches_WinnerIsNull()
    {
        var (circleId, _, tokens) = await SetupAsync();

        var body = await GetAwardsAsync(circleId, tokens[0]);

        Assert.Equal(JsonValueKind.Null, body.GetProperty("currentMonth").GetProperty("winner").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("currentYear").GetProperty("winner").ValueKind);
    }

    // Tie-break: stessa net gain → più partite vince
    [Fact]
    public async Task GetAwards_TieBreak_MoreMatchesWins()
    {
        var (circleId, ids, tokens) = await SetupAsync();

        // Match 1: ids[0]=+10, ids[1]=+10, ids[2]=-10, ids[3]=-10
        await SeedMatchAsync(circleId, ids, ThisMonth, deltaT1P1: 10, deltaT1P2: 10, deltaT2P1: -10, deltaT2P2: -10);
        // Match 2 (swap teams): ids[2]=+10, ids[3]=+10, ids[0]=-10, ids[1]=-10
        await SeedMatchAsync(circleId, new[] { ids[2], ids[3], ids[0], ids[1] }, ThisMonth, deltaT1P1: 10, deltaT1P2: 10, deltaT2P1: -10, deltaT2P2: -10);
        // All 4 players end at net 0, all with 2 matches played → winner by UserId ASC

        var body = await GetAwardsAsync(circleId, tokens[0]);
        var winner = body.GetProperty("currentMonth").GetProperty("winner");

        Assert.NotEqual(JsonValueKind.Null, winner.ValueKind);
        Assert.Equal(2, winner.GetProperty("matchesPlayed").GetInt32());
    }

    // Anno corrente: include partite di un altro mese (non corrente) dell'anno
    [Fact]
    public async Task GetAwards_CurrentYear_IncludesOtherMonthMatches()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        await SeedMatchAsync(circleId, ids, OtherMonthThisYear, deltaT1P1: 40, deltaT1P2: 20, deltaT2P1: -20, deltaT2P2: -40);

        var body = await GetAwardsAsync(circleId, tokens[0]);
        var monthWinner = body.GetProperty("currentMonth").GetProperty("winner");
        var yearWinner  = body.GetProperty("currentYear").GetProperty("winner");

        Assert.Equal(JsonValueKind.Null, monthWinner.ValueKind);   // non nel mese corrente
        Assert.NotEqual(JsonValueKind.Null, yearWinner.ValueKind); // ma nell'anno corrente
        Assert.Equal(40, yearWinner.GetProperty("netGain").GetInt32());
    }

    // Anno scorso non conta per l'anno corrente
    [Fact]
    public async Task GetAwards_LastYearMatches_NotCountedInCurrentYear()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        await SeedMatchAsync(circleId, ids, LastYear, deltaT1P1: 100, deltaT1P2: 100, deltaT2P1: -100, deltaT2P2: -100);

        var body = await GetAwardsAsync(circleId, tokens[0]);

        Assert.Equal(JsonValueKind.Null, body.GetProperty("currentYear").GetProperty("winner").ValueKind);
    }

    // 401 non autenticato
    [Fact]
    public async Task GetAwards_Unauthenticated_Returns401()
    {
        var (circleId, _, _) = await SetupAsync();
        _client.DefaultRequestHeaders.Authorization = null;

        var resp = await _client.GetAsync($"/circles/{circleId}/awards");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // 404 circolo inesistente
    [Fact]
    public async Task GetAwards_NonExistentCircle_Returns404()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);

        var resp = await _client.GetAsync($"/circles/{Guid.NewGuid()}/awards");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupAsync()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse((await circleResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        var (p2Id, p2Token) = await RegisterAndJoinAsync(circleId);
        var (p3Id, p3Token) = await RegisterAndJoinAsync(circleId);
        var (p4Id, p4Token) = await RegisterAndJoinAsync(circleId);

        return (circleId,
            new[] { ExtractUserIdFromJwt(ownerToken), p2Id, p3Id, p4Id },
            new[] { ownerToken, p2Token, p3Token, p4Token });
    }

    private async Task SeedMatchAsync(
        Guid circleId, Guid[] ids, DateTimeOffset createdAt,
        int deltaT1P1, int deltaT1P2, int deltaT2P1, int deltaT2P2)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Matches.Add(new Match
        {
            CircleId          = circleId,
            CreatedById       = ids[0],
            Status            = "confirmed",
            WinnerTeam        = 1,
            Team1Player1Id    = ids[0],
            Team1Player2Id    = ids[1],
            Team2Player1Id    = ids[2],
            Team2Player2Id    = ids[3],
            CreatedAt         = createdAt,
            DeltaTeam1Player1 = deltaT1P1,
            DeltaTeam1Player2 = deltaT1P2,
            DeltaTeam2Player1 = deltaT2P1,
            DeltaTeam2Player2 = deltaT2P2,
        });
        await db.SaveChangesAsync();
    }

    private async Task<JsonElement> GetAwardsAsync(Guid circleId, string token)
    {
        SetAuth(token);
        return await _client.GetFromJsonAsync<JsonElement>($"/circles/{circleId}/awards");
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

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static Guid ExtractUserIdFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class AwardsTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"AwardsTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.AddSingleton<IRatingService, TestRatingService>();
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
