using System.Net;
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

public class AccountDeletionIntegrationTests : IClassFixture<AccountDeletionTestFactory>
{
    private const string Password = "password123";
    private readonly HttpClient _client;
    private readonly AccountDeletionTestFactory _factory;

    public AccountDeletionIntegrationTests(AccountDeletionTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // AC1 — password errata → 401, nessuna modifica DB
    [Fact]
    public async Task DeleteAccount_WrongPassword_Returns401_NoChanges()
    {
        var (_, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var resp = await _client.PostAsJsonAsync("/auth/me/delete", new { password = "wrongpassword" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync(ids[0]);
        Assert.NotEqual("Utente eliminato", user!.Name);
    }

    // AC2/AC3 — password corretta → anonimizzato, login con vecchie credenziali fallisce
    [Fact]
    public async Task DeleteAccount_CorrectPassword_AnonymizesAndBlocksOldLogin()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        var oldEmail = await GetEmailAsync(ids[0]);
        SetAuth(tokens[0]);

        var resp = await _client.PostAsJsonAsync("/auth/me/delete", new { password = Password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync(ids[0]);
        Assert.Equal("Utente eliminato", user!.Name);
        Assert.NotEqual(oldEmail, user.Email);

        var loginResp = await _client.PostAsJsonAsync("/auth/login", new { email = oldEmail, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, loginResp.StatusCode);
    }

    // AC4 — membership rimossa in tutti i circoli
    [Fact]
    public async Task DeleteAccount_RemovesCircleMembershipsEverywhere()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);
        await _client.PostAsJsonAsync("/auth/me/delete", new { password = Password });

        SetAuth(tokens[1]);
        var membersResp = await _client.GetAsync($"/circles/{circleId}/members");
        var members = await membersResp.Content.ReadFromJsonAsync<JsonElement>();
        var stillMember = members.EnumerateArray().Any(m => Guid.Parse(m.GetProperty("userId").GetString()!) == ids[0]);
        Assert.False(stillMember);
    }

    // AC6 — match pending con l'utente eliminato → cancelled; pending senza l'utente → invariato
    [Fact]
    public async Task DeleteAccount_CancelsPendingMatches_WithDeletedUser_LeavesOthersUntouched()
    {
        var (circleId, ids, tokens) = await SetupAsync();

        var matchWithDeletedUser = await InsertMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3], "pending");
        var (otherUserId, _) = await RegisterAndJoinAsync(circleId);
        var matchWithoutDeletedUser = await InsertMatchAsync(circleId, ids[1], ids[2], ids[3], otherUserId, "pending");

        SetAuth(tokens[0]);
        await _client.PostAsJsonAsync("/auth/me/delete", new { password = Password });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("cancelled", (await db.Matches.FindAsync(matchWithDeletedUser))!.Status);
        Assert.Equal("pending", (await db.Matches.FindAsync(matchWithoutDeletedUser))!.Status);
    }

    // AC5 — match confirmed storico resta intatto (status, delta) dopo l'eliminazione di un partecipante
    [Fact]
    public async Task DeleteAccount_LeavesConfirmedMatchesIntact()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        var confirmedMatchId = await InsertMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3], "confirmed", delta: 16);

        SetAuth(tokens[0]);
        await _client.PostAsJsonAsync("/auth/me/delete", new { password = Password });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = await db.Matches.FindAsync(confirmedMatchId);
        Assert.Equal("confirmed", match!.Status);
        Assert.Equal(16, match.DeltaTeam1Player1);
    }

    // AC7 — sessioni invalidate: token già emesso (access + refresh) non funziona più dopo il delete
    [Fact]
    public async Task DeleteAccount_InvalidatesExistingTokens()
    {
        var (_, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var refreshResp = await _client.PostAsJsonAsync("/auth/login", new { email = await GetEmailAsync(ids[0]), password = Password });
        var refreshBody = await refreshResp.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = refreshBody.GetProperty("refreshToken").GetString()!;
        var accessToken = refreshBody.GetProperty("accessToken").GetString()!;

        SetAuth(accessToken);
        await _client.PostAsJsonAsync("/auth/me/delete", new { password = Password });

        using var reusedRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout-all");
        reusedRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var reusedResponse = await _client.SendAsync(reusedRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, reusedResponse.StatusCode);

        var refreshAfterDelete = await _client.PostAsJsonAsync("/auth/refresh", new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterDelete.StatusCode);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupAsync()
    {
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport = "padel" });
        var circleBody = await circleResp.Content.ReadFromJsonAsync<JsonElement>();
        var circleId = Guid.Parse(circleBody.GetProperty("id").GetString()!);

        var (p2Id, p2Token) = await RegisterAndJoinAsync(circleId);
        var (p3Id, p3Token) = await RegisterAndJoinAsync(circleId);
        var (p4Id, p4Token) = await RegisterAndJoinAsync(circleId);

        var ownerId = ExtractUserIdFromJwt(ownerToken);

        SetAuth(ownerToken);
        return (circleId, new[] { ownerId, p2Id, p3Id, p4Id }, new[] { ownerToken, p2Token, p3Token, p4Token });
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
            new { name = "Player", email, password = Password });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private async Task<string> GetEmailAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FindAsync(userId);
        return user!.Email;
    }

    private async Task<Guid> InsertMatchAsync(Guid circleId, Guid t1p1, Guid t1p2, Guid t2p1, Guid t2p2, string status, int? delta = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var match = new Match
        {
            CircleId = circleId,
            CreatedById = t1p1,
            Status = status,
            WinnerTeam = 1,
            Team1Player1Id = t1p1,
            Team1Player2Id = t1p2,
            Team2Player1Id = t2p1,
            Team2Player2Id = t2p2,
            DeltaTeam1Player1 = delta,
        };
        db.Matches.Add(match);
        await db.SaveChangesAsync();
        return match.Id;
    }

    private static Guid ExtractUserIdFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

public class AccountDeletionTestFactory : WebApplicationFactory<Program>
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
            var dbName = $"AccountDeletionTestDb_{Guid.NewGuid()}";
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
