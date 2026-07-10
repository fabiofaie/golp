using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Golp.Tests.Integration;

public class AdminEndpointsTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public AdminEndpointsTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static string UniqueEmail() => $"admin-{Guid.NewGuid():N}@test.com";

    [Fact]
    public async Task WhoAmI_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/admin/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WhoAmI_NormalUser_Returns403()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/admin/whoami");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task WhoAmI_SuperAdmin_Returns200()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Admin", email, password = "password123" });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.IsSuperAdmin = true;
            await db.SaveChangesAsync();
        }

        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.GetAsync("/admin/whoami");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, responseBody.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Impersonate_EmptyEmail_Returns400()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.PostAsJsonAsync("/admin/impersonate", new { email = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Impersonate_NormalUser_Returns403()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/admin/impersonate", new { email = UniqueEmail() });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Impersonate_UnknownEmail_Returns404()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.PostAsJsonAsync("/admin/impersonate", new { email = UniqueEmail() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Impersonate_ExistingUser_Returns200WithImpersonatorClaim()
    {
        var adminId = Guid.Empty;
        var adminToken = await RegisterSuperAdminAndLoginAsync(id => adminId = id);

        var targetEmail = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Target", email = targetEmail, password = "password123" });

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.PostAsJsonAsync("/admin/impersonate", new { email = targetEmail });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var impersonationToken = body.GetProperty("token").GetString();

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(impersonationToken);

        Assert.Equal(targetEmail, jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(adminId.ToString(), jwt.Claims.First(c => c.Type == "impersonator_id").Value);
    }

    [Fact]
    public async Task Impersonate_Success_CreatesOpenAuditLog()
    {
        var adminId = Guid.Empty;
        var adminToken = await RegisterSuperAdminAndLoginAsync(id => adminId = id);

        var targetEmail = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Target", email = targetEmail, password = "password123" });

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        await _client.PostAsJsonAsync("/admin/impersonate", new { email = targetEmail });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = await db.Users.FirstAsync(u => u.Email == targetEmail);
        var log = await db.ImpersonationAuditLogs
            .FirstOrDefaultAsync(l => l.SuperAdminId == adminId && l.TargetUserId == target.Id);

        Assert.NotNull(log);
        Assert.Null(log!.EndedAt);
    }

    [Fact]
    public async Task EndImpersonation_NoImpersonatorClaim_Returns403()
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsync("/admin/impersonate/end", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EndImpersonation_ClosesOpenAuditLog()
    {
        var adminId = Guid.Empty;
        var adminToken = await RegisterSuperAdminAndLoginAsync(id => adminId = id);

        var targetEmail = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Target", email = targetEmail, password = "password123" });

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var impersonateResponse = await _client.PostAsJsonAsync("/admin/impersonate", new { email = targetEmail });
        var impersonateBody = await impersonateResponse.Content.ReadFromJsonAsync<JsonElement>();
        var impersonationToken = impersonateBody.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", impersonationToken);
        var endResponse = await _client.PostAsync("/admin/impersonate/end", null);

        Assert.Equal(HttpStatusCode.OK, endResponse.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var target = await db.Users.FirstAsync(u => u.Email == targetEmail);
        var log = await db.ImpersonationAuditLogs
            .FirstAsync(l => l.SuperAdminId == adminId && l.TargetUserId == target.Id);

        Assert.NotNull(log.EndedAt);
    }

    [Fact]
    public async Task EndImpersonation_CalledTwice_IsIdempotent()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();

        var targetEmail = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Target", email = targetEmail, password = "password123" });

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var impersonateResponse = await _client.PostAsJsonAsync("/admin/impersonate", new { email = targetEmail });
        var impersonateBody = await impersonateResponse.Content.ReadFromJsonAsync<JsonElement>();
        var impersonationToken = impersonateBody.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", impersonationToken);
        await _client.PostAsync("/admin/impersonate/end", null);
        var secondEnd = await _client.PostAsync("/admin/impersonate/end", null);

        Assert.Equal(HttpStatusCode.OK, secondEnd.StatusCode);
    }

    // ─── US-061: DELETE /admin/circles/{circleId}/matches/{matchId} ────────────────

    [Fact]
    public async Task DeleteMatch_NormalUser_Returns403()
    {
        var (circleId, matchId, _) = await SeedConfirmedMatchAsync();
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.DeleteAsync($"/admin/circles/{circleId}/matches/{matchId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMatch_UnknownMatch_Returns404()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.DeleteAsync($"/admin/circles/{Guid.NewGuid()}/matches/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteMatch_ConfirmedEloMatch_RebuildsCircleHistoryAndRemovesMatch()
    {
        var adminId = Guid.Empty;
        var adminToken = await RegisterSuperAdminAndLoginAsync(id => adminId = id);

        // Due partite confermate consecutive: la prima fa salire team1, la seconda (da cancellare)
        // lo riporta giù. Cancellandola, il rating di team1 deve restare sul vantaggio della prima.
        var (circleId, matchId1, playerIds) = await SeedConfirmedMatchAsync(winnerTeam: 1);
        var matchId2 = await SeedAdditionalConfirmedMatchAsync(circleId, playerIds, winnerTeam: 2);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rating = scope.ServiceProvider.GetRequiredService<Golp.Api.Services.IRatingService>();
            await rating.CalculateAndApplyAsync(matchId1, db);
            await db.SaveChangesAsync();
            await rating.CalculateAndApplyAsync(matchId2, db);
            await db.SaveChangesAsync();
        }

        int ratingTeam1BeforeDelete;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ratingTeam1BeforeDelete = (await db.CircleMemberships.AsNoTracking()
                .SingleAsync(m => m.CircleId == circleId && m.UserId == playerIds[0])).Rating;
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.DeleteAsync($"/admin/circles/{circleId}/matches/{matchId2}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        // AC7: partita e set orfani rimossi
        Assert.False(await verifyDb.Matches.AnyAsync(m => m.Id == matchId2));
        Assert.False(await verifyDb.MatchSets.AnyAsync(s => s.MatchId == matchId2));

        // AC3: rating ricostruito senza la partita cancellata, non un semplice stato pre-delete
        var ratingTeam1AfterDelete = (await verifyDb.CircleMemberships.AsNoTracking()
            .SingleAsync(m => m.CircleId == circleId && m.UserId == playerIds[0])).Rating;
        Assert.True(ratingTeam1AfterDelete > ratingTeam1BeforeDelete);

        // AC6: audit log presente con snapshot
        var log = await verifyDb.MatchDeletionAuditLogs
            .SingleAsync(l => l.MatchId == matchId2 && l.SuperAdminId == adminId);
        Assert.Contains("\"WinnerTeam\":2", log.MatchSnapshotJson);
    }

    [Fact]
    public async Task DeleteMatch_ConfirmedGameBonusMatch_NoRatingTouchedAndMatchRemoved()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();
        var (circleId, matchId, playerIds) = await SeedConfirmedMatchAsync(ratingMethod: "GameBonus");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gameBonus = scope.ServiceProvider.GetRequiredService<Golp.Api.Services.IGameBonusRatingService>();
            await gameBonus.CalculateAndApplyAsync(matchId, db);
            await db.SaveChangesAsync();
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.DeleteAsync($"/admin/circles/{circleId}/matches/{matchId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scopeVerify = _factory.Services.CreateScope();
        var verifyDb = scopeVerify.ServiceProvider.GetRequiredService<AppDbContext>();

        // AC4: nessun rating toccato per Game+Bonus (campo non usato dal metodo)
        var ratings = await verifyDb.CircleMemberships.AsNoTracking()
            .Where(m => m.CircleId == circleId)
            .Select(m => m.Rating)
            .ToListAsync();
        Assert.All(ratings, r => Assert.Equal(1000, r));
        Assert.False(await verifyDb.Matches.AnyAsync(m => m.Id == matchId));
    }

    [Fact]
    public async Task DeleteMatch_PendingMatch_NoRatingReplayJustRemoved()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();
        var (circleId, matchId, _) = await SeedConfirmedMatchAsync(status: "pending");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.DeleteAsync($"/admin/circles/{circleId}/matches/{matchId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Matches.AnyAsync(m => m.Id == matchId));
    }

    // ─── US-062: PUT /admin/circles/{circleId}/matches/{matchId}/result ────────────

    [Fact]
    public async Task EditMatchResult_NormalUser_Returns403()
    {
        var (circleId, matchId, _) = await SeedConfirmedMatchAsync();
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "User", email, password = "password123" });
        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("token").GetString();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PutAsJsonAsync($"/admin/circles/{circleId}/matches/{matchId}/result",
            new { sets = new[] { new { team1 = 6, team2 = 2 } } });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task EditMatchResult_UnknownMatch_Returns404()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

        var response = await _client.PutAsJsonAsync(
            $"/admin/circles/{Guid.NewGuid()}/matches/{Guid.NewGuid()}/result",
            new { sets = new[] { new { team1 = 6, team2 = 2 } } });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EditMatchResult_PendingMatch_Returns409()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();
        var (circleId, matchId, _) = await SeedConfirmedMatchAsync(status: "pending");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.PutAsJsonAsync($"/admin/circles/{circleId}/matches/{matchId}/result",
            new { sets = new[] { new { team1 = 6, team2 = 2 } } });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task EditMatchResult_InvalidSets_Returns400()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();
        var (circleId, matchId, _) = await SeedConfirmedMatchAsync();

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        // punteggio negativo non ammesso (AC8)
        var response = await _client.PutAsJsonAsync($"/admin/circles/{circleId}/matches/{matchId}/result",
            new { sets = new[] { new { team1 = -3, team2 = 6 } } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EditMatchResult_ConfirmedEloMatch_RecomputesWinnerAndRebuildsHistory()
    {
        var adminId = Guid.Empty;
        var adminToken = await RegisterSuperAdminAndLoginAsync(id => adminId = id);

        // Due partite confermate: la prima (da modificare) team1 vince largo, la seconda team1 vince di misura.
        var (circleId, matchId1, playerIds) = await SeedConfirmedMatchAsync(winnerTeam: 1);
        var matchId2 = await SeedAdditionalConfirmedMatchAsync(circleId, playerIds, winnerTeam: 1);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rating = scope.ServiceProvider.GetRequiredService<Golp.Api.Services.IRatingService>();
            await rating.CalculateAndApplyAsync(matchId1, db);
            await db.SaveChangesAsync();
            await rating.CalculateAndApplyAsync(matchId2, db);
            await db.SaveChangesAsync();
        }

        int ratingTeam1Before;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ratingTeam1Before = (await db.CircleMemberships.AsNoTracking()
                .SingleAsync(m => m.CircleId == circleId && m.UserId == playerIds[0])).Rating;
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        // Ribalta il risultato di match1: ora vince team2 (era team1)
        var response = await _client.PutAsJsonAsync($"/admin/circles/{circleId}/matches/{matchId1}/result",
            new { sets = new[] { new { team1 = 2, team2 = 6 } } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // AC3: WinnerTeam ricalcolato server-side dai nuovi set, non accettato in input
        Assert.Equal(2, body.GetProperty("winnerTeam").GetInt32());

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        // AC4: storia ricostruita, non un semplice delta compensativo — team1 ora deve avere
        // un rating più basso di prima (ha perso invece di vincere la prima partita)
        var ratingTeam1After = (await verifyDb.CircleMemberships.AsNoTracking()
            .SingleAsync(m => m.CircleId == circleId && m.UserId == playerIds[0])).Rating;
        Assert.True(ratingTeam1After < ratingTeam1Before);

        // AC7: audit log con snapshot prima/dopo
        var log = await verifyDb.MatchResultEditAuditLogs
            .SingleAsync(l => l.MatchId == matchId1 && l.SuperAdminId == adminId);
        Assert.Contains("\"WinnerTeam\":1", log.PreviousResultJson);
        Assert.Contains("\"WinnerTeam\":2", log.NewResultJson);
    }

    [Fact]
    public async Task EditMatchResult_ConfirmedGameBonusMatch_RecomputesPointsForSubsequentMatches()
    {
        var adminToken = await RegisterSuperAdminAndLoginAsync();
        var (circleId, matchId1, playerIds) = await SeedConfirmedMatchAsync(winnerTeam: 1, ratingMethod: "GameBonus");
        var matchId2 = await SeedAdditionalConfirmedMatchAsync(circleId, playerIds, winnerTeam: 2);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gameBonus = scope.ServiceProvider.GetRequiredService<Golp.Api.Services.IGameBonusRatingService>();

            // Margine ampio (20-1) per match1: dà a team1 un punteggio Game+Bonus alto, che a sua volta
            // gonfia il bonus upset ricevuto da team2 in match2. Ridurlo poi a 6-5 nell'edit deve
            // ridurre visibilmente quel bonus — con margini piccoli entrambi arrotondano a bonus=1
            // (ceil(0.1*x) satura), mascherando la regressione che questo test vuole verificare.
            var match1Sets = await db.MatchSets.Where(s => s.MatchId == matchId1).ToListAsync();
            db.MatchSets.RemoveRange(match1Sets);
            db.MatchSets.Add(new MatchSet { MatchId = matchId1, SetNumber = 1, Team1Score = 20, Team2Score = 1 });
            await db.SaveChangesAsync();

            await gameBonus.CalculateAndApplyAsync(matchId1, db);
            await db.SaveChangesAsync();
            await gameBonus.CalculateAndApplyAsync(matchId2, db);
            await db.SaveChangesAsync();
        }

        int? match2PointsBefore;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            match2PointsBefore = (await db.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId2)).GameBonusWinnerPoints;
        }

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await _client.PutAsJsonAsync($"/admin/circles/{circleId}/matches/{matchId1}/result",
            new { sets = new[] { new { team1 = 6, team2 = 5 } } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyDb2 = _factory.Services.CreateScope().ServiceProvider.GetRequiredService<AppDbContext>();
        // AC5: il punteggio della seconda partita (non toccata direttamente) riflette il nuovo bonus,
        // perché dipendeva dallo snapshot dei punti di match1 al momento del suo calcolo originale.
        var match2PointsAfter = (await verifyDb2.Matches.AsNoTracking().SingleAsync(m => m.Id == matchId2)).GameBonusWinnerPoints;
        Assert.NotEqual(match2PointsBefore, match2PointsAfter);
    }

    private async Task<(Guid CircleId, Guid MatchId, Guid[] PlayerIds)> SeedConfirmedMatchAsync(
        string status = "confirmed", int winnerTeam = 1, string ratingMethod = "Elo")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var circleId = Guid.NewGuid();
        var playerIds = new Guid[4];
        for (int i = 0; i < 4; i++)
        {
            var u = new User { Name = $"Player{i}", Email = UniqueEmail(), PasswordHash = "x" };
            db.Users.Add(u);
            playerIds[i] = u.Id;
        }

        db.Circles.Add(new Circle
        {
            Id = circleId, OwnerId = playerIds[0], Name = $"Circle-{circleId:N}",
            Sport = "padel", PointUnit = "games", Sets = true, TeamSize = 2, RatingMethod = ratingMethod,
        });
        foreach (var pid in playerIds)
            db.CircleMemberships.Add(new CircleMembership { CircleId = circleId, UserId = pid, Rating = 1000 });

        var match = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = status, WinnerTeam = winnerTeam,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
        };
        db.Matches.Add(match);
        db.MatchSets.AddRange(
            winnerTeam == 1
                ? new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 6, Team2Score = 2 }
                : new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 2, Team2Score = 6 });

        await db.SaveChangesAsync();
        return (circleId, match.Id, playerIds);
    }

    private async Task<Guid> SeedAdditionalConfirmedMatchAsync(Guid circleId, Guid[] playerIds, int winnerTeam)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var match = new Match
        {
            CircleId = circleId, CreatedById = playerIds[0], Status = "confirmed", WinnerTeam = winnerTeam,
            Team1Player1Id = playerIds[0], Team1Player2Id = playerIds[1],
            Team2Player1Id = playerIds[2], Team2Player2Id = playerIds[3],
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(1),
        };
        db.Matches.Add(match);
        db.MatchSets.Add(winnerTeam == 1
            ? new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 6, Team2Score = 2 }
            : new MatchSet { MatchId = match.Id, SetNumber = 1, Team1Score = 2, Team2Score = 6 });

        await db.SaveChangesAsync();
        return match.Id;
    }

    private async Task<string> RegisterSuperAdminAndLoginAsync(Action<Guid>? captureId = null)
    {
        var email = UniqueEmail();
        await _client.PostAsJsonAsync("/auth/register",
            new { name = "Admin", email, password = "password123" });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.FirstAsync(u => u.Email == email);
            user.IsSuperAdmin = true;
            await db.SaveChangesAsync();
            captureId?.Invoke(user.Id);
        }

        var login = await _client.PostAsJsonAsync("/auth/login",
            new { email, password = "password123" });
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }
}
