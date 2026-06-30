using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Golp.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Golp.Tests.Integration;

public class DisputeMatchTests : IClassFixture<DisputeMatchTestFactory>
{
    private readonly HttpClient _client;
    private readonly DisputeMatchTestFactory _factory;

    public DisputeMatchTests(DisputeMatchTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // Dispute → status = disputed
    [Fact]
    public async Task DisputeMatch_Returns200StatusDisputed()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("disputed", body.GetProperty("status").GetString());
    }

    // Dispute → IRatingService mai chiamato
    [Fact]
    public async Task DisputeMatch_RatingServiceNeverCalled()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);

        Assert.False(_factory.DisputeRatingService.WasCalledWith(matchId));
    }

    // Dispute su confirmed → 409
    [Fact]
    public async Task DisputeMatch_AlreadyConfirmed_Returns409()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        var matchId = await CreateAndFullyConfirmAsync(circleId, ids, tokens);

        SetAuth(tokens[0]);
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
    }

    // Dispute su disputed → 409
    [Fact]
    public async Task DisputeMatch_AlreadyDisputed_Returns409()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);
        var r2 = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
    }

    // US-020 AC5/AC6 — dispute notifica via email l'owner del circolo, fallimento invio non blocca la dispute
    [Fact]
    public async Task DisputeMatch_SendsEmailToCircleOwner()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        var ownerEmail = ExtractEmailFromJwt(tokens[0]);
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        SetAuth(tokens[1]);
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);

        await _factory.EmailCapture.WaitUntilCountAsync(
            () => _factory.EmailCapture.DisputeNotificationsSent.Count(s => s.MatchLink.Contains(matchId.ToString())), 1, TimeSpan.FromSeconds(5));

        var sent = _factory.EmailCapture.DisputeNotificationsSent.Where(s => s.MatchLink.Contains(matchId.ToString())).ToList();
        Assert.Single(sent);
        Assert.Equal(ownerEmail, sent[0].Email);
        Assert.Contains($"/circles/{circleId}/matches/{matchId}", sent[0].MatchLink);
    }

    // US-020 AC6 — fallimento invio email non blocca la dispute
    [Fact]
    public async Task DisputeMatch_EmailSendFails_DisputeStillSucceeds()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        _factory.EmailCapture.ShouldThrow = true;
        try
        {
            SetAuth(tokens[1]);
            var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);

            await _factory.EmailCapture.WaitUntilCountAsync(
                () => _factory.EmailCapture.DisputeNotificationsSent.Count(s => s.MatchLink.Contains(matchId.ToString())), 1, TimeSpan.FromSeconds(5));
            Assert.Equal(1, _factory.EmailCapture.DisputeNotificationsSent.Count(s => s.MatchLink.Contains(matchId.ToString())));
        }
        finally
        {
            _factory.EmailCapture.ShouldThrow = false;
        }
    }

    // Non-partecipante → 403
    [Fact]
    public async Task DisputeMatch_NonParticipant_Returns403()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        var outsiderToken = await RegisterTokenAsync();
        SetAuth(outsiderToken);
        await _client.PostAsync($"/circles/{circleId}/join", null);
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);
        Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode);
    }

    // 401 non-autenticato
    [Fact]
    public async Task DisputeMatch_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var r = await _client.PostAsync($"/circles/{Guid.NewGuid()}/matches/{Guid.NewGuid()}/dispute", null);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    // Dispute da qualsiasi partecipante (non solo inseritore) → funziona
    [Fact]
    public async Task DisputeMatch_ByNonCreatorParticipant_Returns200()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        SetAuth(tokens[3]);  // ultimo partecipante, non l'inseritore
        var r = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/dispute", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("disputed", (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString());
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private static Guid GetId(JsonElement el) =>
        Guid.Parse(el.GetProperty("id").GetString()!);

    private async Task<Guid> CreateAndFullyConfirmAsync(Guid circleId, Guid[] ids, string[] tokens)
    {
        SetAuth(tokens[0]);
        var matchId = GetId(await (await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3]))
            .Content.ReadFromJsonAsync<JsonElement>());

        for (var i = 1; i <= 3; i++)
        {
            SetAuth(tokens[i]);
            await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        }

        return matchId;
    }

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupAsync()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport = "padel" });
        var circleId = Guid.Parse((await circleResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!);

        var (p2Id, p2Token) = await RegisterAndJoinAsync(circleId);
        var (p3Id, p3Token) = await RegisterAndJoinAsync(circleId);
        var (p4Id, p4Token) = await RegisterAndJoinAsync(circleId);

        return (circleId,
            new[] { ExtractUserIdFromJwt(ownerToken), p2Id, p3Id, p4Id },
            new[] { ownerToken, p2Token, p3Token, p4Token });
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

    private static Guid ExtractUserIdFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private static string ExtractEmailFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.GetProperty("email").GetString()!;
    }

    private Task<HttpResponseMessage> PostMatchAsync(Guid circleId, Guid t1p1, Guid t1p2, Guid t2p1, Guid t2p2) =>
        _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = t1p1 }, new { userId = t1p2 } },
            team2 = new[] { new { userId = t2p1 }, new { userId = t2p2 } },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class DisputeMatchTestFactory : WebApplicationFactory<Program>
{
    public TestDisputeRatingService DisputeRatingService { get; } = new();
    public TestEmailCapture EmailCapture { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true
                         || d.ServiceType == typeof(Golp.Api.Services.IEmailService))
                .ToList();
            foreach (var d in toRemove)
                services.Remove(d);

            services.AddEntityFrameworkInMemoryDatabase();
            var dbName = $"DisputeTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.AddSingleton<IRatingService>(DisputeRatingService);

            services.AddSingleton(EmailCapture);
            services.AddScoped<Golp.Api.Services.IEmailService>(sp => sp.GetRequiredService<TestEmailCapture>());
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

public class TestDisputeRatingService : IRatingService
{
    private readonly System.Collections.Concurrent.ConcurrentBag<Guid> _called = [];

    public bool WasCalledWith(Guid matchId) => _called.Contains(matchId);

    public Task<IReadOnlyList<(Guid UserId, int NewPosition)>> CalculateAndApplyAsync(Guid matchId, AppDbContext db)
    {
        _called.Add(matchId);
        return Task.FromResult<IReadOnlyList<(Guid, int)>>([]);
    }
}
