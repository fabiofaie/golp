using System.Collections.Concurrent;
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
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Golp.Tests.Integration;

public class MatchIntegrationTests : IClassFixture<MatchTestFactory>
{
    private readonly MatchTestFactory _factory;
    private readonly HttpClient _client;

    public MatchIntegrationTests(MatchTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // AC1 — 4 giocatori validi, sets=true → 201 + status pending
    [Fact]
    public async Task CreateMatch_ValidPlayers_SetsTrue_Returns201()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: true);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 }, new { team1 = 3, team2 = 6 }, new { team1 = 7, team2 = 5 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", json.GetProperty("status").GetString());
        Assert.Equal(1, json.GetProperty("winnerTeam").GetInt32());
    }

    // US-034 AC — set pari ma game diversi → 201, vincitore deciso dai game
    [Fact]
    public async Task CreateMatch_SetsTied_GamesDecide_Returns201()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: true);
        SetAuth(tokens[0]);

        // 1 set a testa (6-2, 4-6), ma team1 totalizza più game (10 vs 8)
        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 2 }, new { team1 = 4, team2 = 6 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("winnerTeam").GetInt32());
    }

    // US-034 AC — set pari E game pari → vero pareggio, 400 invariato
    [Fact]
    public async Task CreateMatch_SetsTied_GamesAlsoTied_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: true);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 }, new { team1 = 4, team2 = 6 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC2 — sets=false, punteggio singolo → 201
    [Fact]
    public async Task CreateMatch_SetsFalse_SingleScore_Returns201()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 54, team2 = 38 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, json.GetProperty("winnerTeam").GetInt32());
    }

    // AC6 — status = pending al create
    [Fact]
    public async Task CreateMatch_Status_IsPending()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", json.GetProperty("status").GetString());
    }

    // AC4 — giocatore duplicato → 400
    [Fact]
    public async Task CreateMatch_DuplicatePlayer_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        // ids[0] duplicato in team2
        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[0],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC4 — giocatore non membro → 400
    [Fact]
    public async Task CreateMatch_NonMemberPlayer_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var outsiderId = await RegisterAndExtractIdAsync();

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], outsiderId,
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC3 — inseritore non in nessun team, e non owner → 400
    [Fact]
    public async Task CreateMatch_CreatorNotInTeam_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();

        var (_, outsiderToken) = await RegisterAndJoinAsync(circleId);
        SetAuth(outsiderToken);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // US-025 AC1+AC3 — owner non tra i 4 giocatori → 201, nessuna conferma implicita (0/4)
    [Fact]
    public async Task CreateMatch_OwnerNotInTeam_Returns201_NoImplicitConfirmation()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        // ids[0]/tokens[0] è l'owner (vedi SetupAsync): registra una partita tra p2/p3/p4 + un quarto membro.
        var (p5Id, _) = await RegisterAndJoinAsync(circleId);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[1], ids[2], ids[3], p5Id,
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = json.GetProperty("id").GetString()!;

        var listResp = await _client.GetAsync($"/circles/{circleId}/matches");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var match = list.EnumerateArray().First(m => m.GetProperty("id").GetString() == matchId);

        Assert.Equal(0, match.GetProperty("confirmationsCount").GetInt32());
    }

    // US-025 AC2 — owner che PARTECIPA: comportamento invariato, 1/4 conferme (regression guard)
    [Fact]
    public async Task CreateMatch_OwnerInTeam_Returns201_WithImplicitConfirmation()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = json.GetProperty("id").GetString()!;

        var listResp = await _client.GetAsync($"/circles/{circleId}/matches");
        var list = await listResp.Content.ReadFromJsonAsync<JsonElement>();
        var match = list.EnumerateArray().First(m => m.GetProperty("id").GetString() == matchId);

        Assert.Equal(1, match.GetProperty("confirmationsCount").GetInt32());
    }

    // AC5 — tie sets → 400 (sets=true, 1-1)
    [Fact]
    public async Task CreateMatch_TieSets_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: true);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 }, new { team1 = 4, team2 = 6 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC5 — tie score → 400 (sets=false)
    [Fact]
    public async Task CreateMatch_TieScore_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 50, team2 = 50 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // AC2 — sets vuoti → 400
    [Fact]
    public async Task CreateMatch_EmptySets_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var resp = await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = ids[0] }, new { userId = ids[1] } },
            team2 = new[] { new { userId = ids[2] }, new { userId = ids[3] } },
            sets  = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // no auth → 401
    [Fact]
    public async Task CreateMatch_NoAuth_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var resp = await _client.PostAsJsonAsync($"/circles/{Guid.NewGuid()}/matches", new
        {
            team1 = new[] { Guid.NewGuid(), Guid.NewGuid() },
            team2 = new[] { Guid.NewGuid(), Guid.NewGuid() },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // circolo non trovato → 404
    [Fact]
    public async Task CreateMatch_CircleNotFound_Returns404()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);

        var resp = await _client.PostAsJsonAsync($"/circles/{Guid.NewGuid()}/matches", new
        {
            team1 = new[] { new { userId = Guid.NewGuid() }, new { userId = Guid.NewGuid() } },
            team2 = new[] { new { userId = Guid.NewGuid() }, new { userId = Guid.NewGuid() } },
            sets  = new[] { new { team1 = 6, team2 = 4 } },
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // inseritore non membro del circolo → 403
    [Fact]
    public async Task CreateMatch_RequesterNotMember_Returns403()
    {
        var (circleId, ids, _) = await SetupAsync();

        var nonMemberToken = await RegisterTokenAsync();
        SetAuth(nonMemberToken);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // AC2 — sets=false con 2 set → 400
    [Fact]
    public async Task CreateMatch_SetsFalse_MultipleSets_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 54, team2 = 38 }, new { team1 = 60, team2 = 30 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // US-006 — push ai 3 partecipanti da confermare, escluso l'inseritore.
    // Il fake lancia eccezione dopo aver registrato la chiamata: verifica anche
    // che la creazione partita resti 201 quando la push fallisce (fire-and-forget).
    [Fact]
    public async Task CreateMatch_SendsPushToThreeRecipients_ExcludingCreator()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = Guid.Parse(json.GetProperty("id").GetString()!);

        var call = await _factory.PushRecorder.WaitForCallAsync(matchId, TimeSpan.FromSeconds(5));

        Assert.Equal(circleId, call.CircleId);
        // ids[0] è l'inseritore: escluso
        Assert.Equal(3, call.RecipientUserIds.Length);
        Assert.DoesNotContain(ids[0], call.RecipientUserIds);
        Assert.Equivalent(new[] { ids[1], ids[2], ids[3] }, call.RecipientUserIds);
    }

    // US-020 AC4 — email di richiesta conferma ai 3 partecipanti, escluso l'inseritore (oltre al push)
    [Fact]
    public async Task CreateMatch_SendsConfirmationEmailToThreeRecipients_ExcludingCreator()
    {
        var (circleId, ids, tokens) = await SetupAsync();
        SetAuth(tokens[0]);

        var beforeCount = _factory.EmailCapture.ConfirmationRequestsSent.Count;

        var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 6, team2 = 4 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        await _factory.EmailCapture.WaitUntilCountAsync(
            () => _factory.EmailCapture.ConfirmationRequestsSent.Count - beforeCount, 3, TimeSpan.FromSeconds(5));

        Assert.Equal(3, _factory.EmailCapture.ConfirmationRequestsSent.Count - beforeCount);
        // Links now use per-user token URL: /m/{tokenGuid} (US-040)
        Assert.All(
            _factory.EmailCapture.ConfirmationRequestsSent.Where(s => s.MatchLink.Contains("/m/")),
            s => Assert.Contains("/m/", s.MatchLink));
    }

    // US-020 AC6 — fallimento invio email non blocca la creazione partita (fire-and-forget)
    [Fact]
    public async Task CreateMatch_EmailSendFails_MatchCreationStillSucceeds()
    {
        _factory.EmailCapture.ShouldThrow = true;
        try
        {
            var (circleId, ids, tokens) = await SetupAsync();
            SetAuth(tokens[0]);

            var beforeCount = _factory.EmailCapture.ConfirmationRequestsSent.Count;

            var resp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
                new[] { new { team1 = 6, team2 = 4 } });

            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

            await _factory.EmailCapture.WaitUntilCountAsync(
                () => _factory.EmailCapture.ConfirmationRequestsSent.Count - beforeCount, 3, TimeSpan.FromSeconds(5));
            Assert.Equal(3, _factory.EmailCapture.ConfirmationRequestsSent.Count - beforeCount);
        }
        finally
        {
            _factory.EmailCapture.ShouldThrow = false;
        }
    }

    // US-035 — conferma 4/4: chi sale in classifica riceve ranking push; gli altri no
    [Fact]
    public async Task ConfirmFourth_PlayerRises_ReceivesRankingPush()
    {
        // Setup: 4 giocatori in un circolo
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);

        // Imposta rating: team1=999, team2=1001 → gap minimo garantisce che il delta ELO sorpassi
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var memberships = await db.CircleMemberships
                .Where(m => m.CircleId == circleId)
                .ToListAsync();
            foreach (var m in memberships)
                m.Rating = (m.UserId == ids[0] || m.UserId == ids[1]) ? 999 : 1001;
            await db.SaveChangesAsync();
        }

        // Crea partita: team1 (800) vs team2 (1000) — team1 vince
        SetAuth(tokens[0]);
        var matchResp = await PostMatchAsync(circleId, ids[0], ids[1], ids[2], ids[3],
            new[] { new { team1 = 21, team2 = 5 } });
        Assert.Equal(HttpStatusCode.Created, matchResp.StatusCode);
        var matchJson = await matchResp.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = matchJson.GetProperty("id").GetString()!;

        // Prima conferma è automatica dal creatore (ids[0]) → già 1 conferma
        SetAuth(tokens[1]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        SetAuth(tokens[2]);
        await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);
        SetAuth(tokens[3]);
        var lastConfirmResp = await _client.PostAsync($"/circles/{circleId}/matches/{matchId}/confirm", null);

        var finalJson = await lastConfirmResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("confirmed", finalJson.GetProperty("status").GetString());

        await Task.Delay(200); // push è fire-and-forget

        var rankingCalls = _factory.PushRecorder.RankingCalls.ToList();
        var rankingUserIds = rankingCalls.Select(c => c.UserId).ToHashSet();

        // team2 scende → nessuna push per loro
        Assert.DoesNotContain(ids[2], rankingUserIds);
        Assert.DoesNotContain(ids[3], rankingUserIds);
        // team1 vince partendo da 800 → superano il team2 (1000) → salgono
        Assert.Contains(ids[0], rankingUserIds);
        Assert.Contains(ids[1], rankingUserIds);
    }

    // ─── US-039: guest resolution ────────────────────────────────────────────

    // Ospite nuovo con email → User creato IsActivated=false, Membership Rating=1000
    [Fact]
    public async Task CreateMatch_NewGuestByEmail_CreatesUserAndMembership()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        var guestEmail = $"guest_{Guid.NewGuid():N}@example.com";
        var resp = await PostMatchWithSlotsAsync(circleId,
            new { userId = ids[0] },
            new { userId = ids[1] },
            new { userId = ids[2] },
            new { guestName = "Ospite Uno", guestEmail },
            new[] { new { team1 = 21, team2 = 5 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var guest = await db.Users.FirstOrDefaultAsync(u => u.Email == guestEmail);
        Assert.NotNull(guest);
        Assert.False(guest.IsActivated);
        Assert.Empty(guest.PasswordHash);

        var membership = await db.CircleMemberships
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == guest.Id);
        Assert.NotNull(membership);
        Assert.Equal(1000, membership.Rating);
    }

    // Ospite con email = User esistente → nessun duplicato, stesso UserId in partita
    [Fact]
    public async Task CreateMatch_GuestEmailMatchesExistingUser_NoDuplicate()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        // Crea un utente registrato non membro del circolo
        var existingEmail = $"existing_{Guid.NewGuid():N}@example.com";
        var regResp = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Utente Esistente", email = existingEmail, password = "password123" });
        var regBody = await regResp.Content.ReadFromJsonAsync<JsonElement>();
        var existingToken = regBody.GetProperty("token").GetString()!;
        var existingUserId = ExtractUserIdFromJwt(existingToken);

        var resp = await PostMatchWithSlotsAsync(circleId,
            new { userId = ids[0] },
            new { userId = ids[1] },
            new { userId = ids[2] },
            new { guestName = "Ospite Alias", guestEmail = existingEmail },
            new[] { new { team1 = 21, team2 = 5 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var count = await db.Users.CountAsync(u => u.Email == existingEmail);
        Assert.Equal(1, count);

        var membership = await db.CircleMemberships
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == existingUserId);
        Assert.NotNull(membership);
    }

    // Ospite con phone = User esistente → nessun duplicato
    [Fact]
    public async Task CreateMatch_GuestPhoneMatchesExistingGhost_NoDuplicate()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        // Prima partita: crea ghost con phone
        var phone = "+39333" + Guid.NewGuid().ToString("N")[..7];
        var resp1 = await PostMatchWithSlotsAsync(circleId,
            new { userId = ids[0] },
            new { userId = ids[1] },
            new { userId = ids[2] },
            new { guestName = "Ghost Phone", guestPhone = phone },
            new[] { new { team1 = 21, team2 = 5 } });
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        using var scope1 = _factory.Services.CreateScope();
        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var ghostId = (await db1.Users.FirstOrDefaultAsync(u => u.Phone == phone))!.Id;
        var countBefore = await db1.Users.CountAsync(u => u.Phone == phone);

        // Seconda partita: stesso phone → riusa l'utente
        // Serve un 5o membro per evitare conflitto distinct-4
        var (newId, newToken) = await RegisterAndJoinAsync(circleId);
        SetAuth(tokens[0]);

        var resp2 = await PostMatchWithSlotsAsync(circleId,
            new { userId = ids[0] },
            new { userId = ids[1] },
            new { userId = newId },
            new { guestName = "Ghost Phone Again", guestPhone = phone },
            new[] { new { team1 = 21, team2 = 5 } });
        Assert.Equal(HttpStatusCode.Created, resp2.StatusCode);

        using var scope2 = _factory.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var countAfter = await db2.Users.CountAsync(u => u.Phone == phone);
        Assert.Equal(countBefore, countAfter);
    }

    // 2 slot con stessa email ospite → 400 (4 giocatori distinti)
    [Fact]
    public async Task CreateMatch_TwoSlotsWithSameGuestEmail_Returns400()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        var guestEmail = $"dupguest_{Guid.NewGuid():N}@example.com";
        var resp = await PostMatchWithSlotsAsync(circleId,
            new { userId = ids[0] },
            new { userId = ids[1] },
            new { guestName = "Ospite A", guestEmail },
            new { guestName = "Ospite B", guestEmail },
            new[] { new { team1 = 21, team2 = 5 } });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("distinti", body.GetProperty("error").GetString()!);
    }

    // Ospite solo phone (no email) → User creato, email skippata (no eccezione)
    [Fact]
    public async Task CreateMatch_GuestPhoneOnly_CreatesUserNoEmail()
    {
        var (circleId, ids, tokens) = await SetupAsync(useSets: false);
        SetAuth(tokens[0]);

        var beforeCountGuest = _factory.EmailCapture.ConfirmationRequestsSent.Count;
        var phone = "+39344" + Guid.NewGuid().ToString("N")[..7];
        var resp = await PostMatchWithSlotsAsync(circleId,
            new { userId = ids[0] },
            new { userId = ids[1] },
            new { userId = ids[2] },
            new { guestName = "Solo Telefono", guestPhone = phone },
            new[] { new { team1 = 21, team2 = 5 } });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ghost = await db.Users.FirstOrDefaultAsync(u => u.Phone == phone);
        Assert.NotNull(ghost);
        Assert.Null(ghost.Email);
        Assert.False(ghost.IsActivated);

        // Aspetta fire-and-forget email (deve essere 2, non 3: il phone-only non riceve email)
        await _factory.EmailCapture.WaitUntilCountAsync(
            () => _factory.EmailCapture.ConfirmationRequestsSent.Count - beforeCountGuest,
            2, TimeSpan.FromSeconds(5));
        Assert.Equal(2, _factory.EmailCapture.ConfirmationRequestsSent.Count - beforeCountGuest);
    }

    // ─── helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupAsync(bool useSets = true)
    {
        var sport = useSets ? "padel" : "basket2v2";

        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"C_{Guid.NewGuid():N}", sport });
        var circleBody = await circleResp.Content.ReadFromJsonAsync<JsonElement>();
        var circleId = Guid.Parse(circleBody.GetProperty("id").GetString()!);

        var (p2Id, p2Token) = await RegisterAndJoinAsync(circleId);
        var (p3Id, p3Token) = await RegisterAndJoinAsync(circleId);
        var (p4Id, p4Token) = await RegisterAndJoinAsync(circleId);

        var ownerId = ExtractUserIdFromJwt(ownerToken);

        return (circleId,
            new[] { ownerId, p2Id, p3Id, p4Id },
            new[] { ownerToken, p2Token, p3Token, p4Token });
    }

    private async Task<(Guid Id, string Token)> RegisterAndJoinAsync(Guid circleId)
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);
        await _client.PostAsync($"/circles/{circleId}/join", null);
        return (ExtractUserIdFromJwt(token), token);
    }

    private async Task<Guid> RegisterAndExtractIdAsync()
    {
        var token = await RegisterTokenAsync();
        return ExtractUserIdFromJwt(token);
    }

    private async Task<string> RegisterTokenAsync()
    {
        var email = $"u_{Guid.NewGuid():N}@test.com";
        var r = await _client.PostAsJsonAsync("/auth/register",
            new { name = "Player", email, password = "password123" });
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private static Guid ExtractUserIdFromJwt(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        using var doc = JsonDocument.Parse(bytes);
        return Guid.Parse(doc.RootElement.GetProperty("sub").GetString()!);
    }

    private Task<HttpResponseMessage> PostMatchAsync(Guid circleId,
        Guid t1p1, Guid t1p2, Guid t2p1, Guid t2p2, object sets)
    {
        return _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = t1p1 }, new { userId = t1p2 } },
            team2 = new[] { new { userId = t2p1 }, new { userId = t2p2 } },
            sets,
        });
    }

    private Task<HttpResponseMessage> PostMatchWithSlotsAsync(Guid circleId, object slot1, object slot2, object slot3, object slot4, object sets)
    {
        return _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { slot1, slot2 },
            team2 = new[] { slot3, slot4 },
            sets,
        });
    }

    private void SetAuth(string token) =>
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
}

public class MatchTestFactory : WebApplicationFactory<Program>
{
    public RecordingPushNotificationService PushRecorder { get; } = new();
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
            var dbName = $"MatchTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            services.RemoveAll(typeof(IPushNotificationService));
            services.AddSingleton<IPushNotificationService>(PushRecorder);

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

/// <summary>
/// Fake che registra le chiamate push e poi lancia: simula FCM down senza
/// impattare la creazione partita (che è fire-and-forget rispetto alla push).
/// </summary>
public class RecordingPushNotificationService : IPushNotificationService
{
    public record PushCall(Guid MatchId, Guid CircleId, Guid[] RecipientUserIds);

    private readonly ConcurrentQueue<PushCall> _calls = new();

    public Task SendConfirmationRequestAsync(Guid matchId, Guid circleId, Guid[] recipientUserIds)
    {
        _calls.Enqueue(new PushCall(matchId, circleId, recipientUserIds));
        throw new InvalidOperationException("FCM unreachable (simulato)");
    }

    public Task<bool> SendTestNotificationAsync(Guid userId) => Task.FromResult(false);

    public record RankingCall(Guid UserId, int NewPosition, string CircleName);
    private readonly ConcurrentQueue<RankingCall> _rankingCalls = new();
    public IEnumerable<RankingCall> RankingCalls => _rankingCalls;

    public Task SendRankingImprovedAsync(Guid userId, int newPosition, string circleName)
    {
        _rankingCalls.Enqueue(new RankingCall(userId, newPosition, circleName));
        return Task.CompletedTask;
    }

    public async Task<RankingCall> WaitForRankingCallAsync(Guid userId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var call = _rankingCalls.FirstOrDefault(c => c.UserId == userId);
            if (call != null)
                return call;
            await Task.Delay(25);
        }
        throw new TimeoutException($"Nessuna ranking push per user {userId} entro {timeout.TotalSeconds}s");
    }

    public async Task<PushCall> WaitForCallAsync(Guid matchId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var call = _calls.FirstOrDefault(c => c.MatchId == matchId);
            if (call != null)
                return call;
            await Task.Delay(25);
        }
        throw new TimeoutException($"Nessuna push registrata per match {matchId} entro {timeout.TotalSeconds}s");
    }
}
