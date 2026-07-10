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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Golp.Tests.Integration;

public class QuickMatchEndpointsTests : IClassFixture<QuickMatchTestFactory>
{
    private readonly QuickMatchTestFactory _factory;
    private readonly HttpClient _client;

    public QuickMatchEndpointsTests(QuickMatchTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ─── Suggestions ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Suggestions_NoHistory_ReturnsEmpty()
    {
        var token = await RegisterTokenAsync();
        SetAuth(token);

        var resp = await _client.GetAsync("/match/quick/suggestions");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task Suggestions_WithMatchAndCircle_ReturnsUnionDeduped()
    {
        // User A in a circle with B; A plays a match with B and C
        var (circleId, ids, tokens) = await SetupAsync("basket2v2");
        // ids: [A=0, B=1, C=2, D=3], all in circle
        // Create a match via circle endpoint to build match history
        SetAuth(tokens[0]);
        await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = ids[0] }, new { userId = ids[1] } },
            team2 = new[] { new { userId = ids[2] }, new { userId = ids[3] } },
            sets  = new[] { new { team1 = 21, team2 = 15 } },
        });

        // Register extra user E in the circle (to test circle-member source)
        var (eId, _) = await RegisterAndJoinAsync(circleId);

        SetAuth(tokens[0]);
        var resp = await _client.GetAsync("/match/quick/suggestions");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var suggestions = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var userIds = Enumerable.Range(0, suggestions.GetArrayLength())
            .Select(i => Guid.Parse(suggestions[i].GetProperty("userId").GetString()!))
            .ToHashSet();

        // B, C, D came from match history; E from circle membership; A excluded (self)
        Assert.Contains(ids[1], userIds);
        Assert.Contains(ids[2], userIds);
        Assert.Contains(ids[3], userIds);
        Assert.Contains(eId, userIds);
        Assert.DoesNotContain(ids[0], userIds); // self excluded
    }

    [Fact]
    public async Task Suggestions_CommonCircles_IncludesCircleIdAndName()
    {
        // A owns circle1 with B; A also owns circle2 with C. B and C should each
        // carry the name of the one circle they share with A.
        var ownerToken = await RegisterTokenAsync();
        SetAuth(ownerToken);

        var circle1Resp = await _client.PostAsJsonAsync("/circles",
            new { name = $"Circolo1_{Guid.NewGuid():N}", sport = "basket2v2" });
        var circle1Body = await circle1Resp.Content.ReadFromJsonAsync<JsonElement>();
        var circle1Id = Guid.Parse(circle1Body.GetProperty("id").GetString()!);
        var circle1Name = circle1Body.GetProperty("name").GetString()!;

        SetAuth(ownerToken);
        var circle2Resp = await _client.PostAsJsonAsync("/circles",
            new { name = $"Circolo2_{Guid.NewGuid():N}", sport = "basket2v2" });
        var circle2Body = await circle2Resp.Content.ReadFromJsonAsync<JsonElement>();
        var circle2Id = Guid.Parse(circle2Body.GetProperty("id").GetString()!);
        var circle2Name = circle2Body.GetProperty("name").GetString()!;

        var (bId, _) = await RegisterAndJoinAsync(circle1Id);
        var (cId, _) = await RegisterAndJoinAsync(circle2Id);

        SetAuth(ownerToken);
        var resp = await _client.GetAsync("/match/quick/suggestions");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var suggestions = await resp.Content.ReadFromJsonAsync<JsonElement>();

        JsonElement Find(Guid userId) =>
            Enumerable.Range(0, suggestions.GetArrayLength())
                .Select(i => suggestions[i])
                .Single(s => Guid.Parse(s.GetProperty("userId").GetString()!) == userId);

        var bEntry = Find(bId);
        var bCircles = bEntry.GetProperty("circles");
        Assert.Equal(1, bCircles.GetArrayLength());
        Assert.Equal(circle1Id, Guid.Parse(bCircles[0].GetProperty("id").GetString()!));
        Assert.Equal(circle1Name, bCircles[0].GetProperty("name").GetString());

        var cEntry = Find(cId);
        var cCircles = cEntry.GetProperty("circles");
        Assert.Equal(1, cCircles.GetArrayLength());
        Assert.Equal(circle2Id, Guid.Parse(cCircles[0].GetProperty("id").GetString()!));
        Assert.Equal(circle2Name, cCircles[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Suggestions_CirclesFieldAlwaysArray()
    {
        var (circleId, ids, tokens) = await SetupAsync("basket2v2");
        SetAuth(tokens[0]);
        await _client.PostAsJsonAsync($"/circles/{circleId}/matches", new
        {
            team1 = new[] { new { userId = ids[0] }, new { userId = ids[1] } },
            team2 = new[] { new { userId = ids[2] }, new { userId = ids[3] } },
            sets  = new[] { new { team1 = 21, team2 = 15 } },
        });

        var resp = await _client.GetAsync("/match/quick/suggestions");
        var suggestions = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(suggestions.GetArrayLength() > 0);
        for (var i = 0; i < suggestions.GetArrayLength(); i++)
        {
            Assert.Equal(JsonValueKind.Array, suggestions[i].GetProperty("circles").ValueKind);
        }
    }

    // ─── Check ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Check_Exact_NoCircle_ReturnsEmpty()
    {
        // 4 users, no shared circle
        var tokens = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegisterTokenAsync()));
        var ids = tokens.Select(ExtractUserIdFromJwt).ToArray();

        SetAuth(tokens[0]);
        var resp = await _client.PostAsJsonAsync("/match/quick/check", new
        {
            sport   = "basket2v2",
            userIds = ids,
            guests  = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("exact", body.GetProperty("mode").GetString());
        Assert.Equal(0, body.GetProperty("circles").GetArrayLength());
    }

    [Fact]
    public async Task Check_Exact_OneCircle_ReturnsExact()
    {
        // 4 users exactly in one basket2v2 circle
        var (_, ids, tokens) = await SetupAsync("basket2v2");

        SetAuth(tokens[0]);
        var resp = await _client.PostAsJsonAsync("/match/quick/check", new
        {
            sport   = "basket2v2",
            userIds = ids,
            guests  = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("exact", body.GetProperty("mode").GetString());
        Assert.Equal(1, body.GetProperty("circles").GetArrayLength());
        // Circle name must be present
        Assert.False(string.IsNullOrEmpty(body.GetProperty("circles")[0].GetProperty("name").GetString()));
    }

    [Fact]
    public async Task Check_Exact_CircleWithMoreThan4Members_StillReturnsExact()
    {
        // Circle has 5 members; check selects only 4 → must still find it
        var (circleId, ids, tokens) = await SetupAsync("basket2v2"); // 4 members
        // Add a 5th member
        var fifthToken = await RegisterTokenAsync();
        var fifthId = ExtractUserIdFromJwt(fifthToken);
        SetAuth(fifthToken);
        await _client.PostAsync($"/circles/{circleId}/join", null);

        // Check with the original 4 IDs (not the 5th)
        SetAuth(tokens[0]);
        var resp = await _client.PostAsJsonAsync("/match/quick/check", new
        {
            sport   = "basket2v2",
            userIds = ids,
            guests  = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("exact", body.GetProperty("mode").GetString());
        Assert.Equal(1, body.GetProperty("circles").GetArrayLength());
    }

    [Fact]
    public async Task Check_Exact_GhostResolvedByEmail_FindsCircle()
    {
        // 3 registered users + 1 ghost (created by email) all in a circle
        var ownerToken = await RegisterTokenAsync();
        var ownerId = ExtractUserIdFromJwt(ownerToken);
        SetAuth(ownerToken);
        var circleResp = await _client.PostAsJsonAsync("/circles",
            new { name = $"G_{Guid.NewGuid():N}", sport = "basket2v2" });
        var circleId = Guid.Parse((await circleResp.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("id").GetString()!);

        var (p2Id, _) = await RegisterAndJoinAsync(circleId);
        var (p3Id, _) = await RegisterAndJoinAsync(circleId);

        // Create ghost directly in DB
        var ghostEmail = $"ghost_{Guid.NewGuid():N}@test.com";
        Guid ghostId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var ghost = new Golp.Api.Data.Entities.User
            {
                Name         = "Ghost",
                Email        = ghostEmail,
                PasswordHash = string.Empty,
                IsActivated  = false,
            };
            db.Users.Add(ghost);
            db.CircleMemberships.Add(new Golp.Api.Data.Entities.CircleMembership
            {
                CircleId = circleId,
                UserId   = ghost.Id,
                Rating   = 1000,
            });
            await db.SaveChangesAsync();
            ghostId = ghost.Id;
        }

        SetAuth(ownerToken);
        var resp = await _client.PostAsJsonAsync("/match/quick/check", new
        {
            sport   = "basket2v2",
            userIds = new[] { ownerId, p2Id, p3Id },
            guests  = new[] { new { email = ghostEmail } },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("exact", body.GetProperty("mode").GetString());
        Assert.Equal(1, body.GetProperty("circles").GetArrayLength());
    }

    [Fact]
    public async Task Check_Partial_NewGuest_ReturnsPartialCircles()
    {
        // 3 registered users in a circle + 1 completely new guest (email not in DB)
        var (circleId, ids, tokens) = await SetupAsync("basket2v2");
        var first3 = ids.Take(3).ToArray();

        SetAuth(tokens[0]);
        var resp = await _client.PostAsJsonAsync("/match/quick/check", new
        {
            sport   = "basket2v2",
            userIds = first3,
            guests  = new[] { new { email = $"new_{Guid.NewGuid():N}@test.com" } },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("partial", body.GetProperty("mode").GetString());
        // The circle contains all 3 known users → should appear
        Assert.True(body.GetProperty("circles").GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Check_Partial_NoKnownCircles_ReturnsEmpty()
    {
        // All 4 are new guests (not in DB) → knownIds=[], mode=partial, circles=[]
        var token = await RegisterTokenAsync();
        SetAuth(token);

        var resp = await _client.PostAsJsonAsync("/match/quick/check", new
        {
            sport   = "basket2v2",
            userIds = Array.Empty<Guid>(),
            guests  = new[]
            {
                new { email = $"new1_{Guid.NewGuid():N}@test.com" },
                new { email = $"new2_{Guid.NewGuid():N}@test.com" },
                new { email = $"new3_{Guid.NewGuid():N}@test.com" },
                new { email = $"new4_{Guid.NewGuid():N}@test.com" },
            },
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("partial", body.GetProperty("mode").GetString());
        Assert.Equal(0, body.GetProperty("circles").GetArrayLength());
    }

    // ─── Quick Match creation ─────────────────────────────────────────────────

    [Fact]
    public async Task QuickMatch_NewCircle_CreatesAtomically_IsPrivate()
    {
        var tokens = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegisterTokenAsync()));
        var ids = tokens.Select(ExtractUserIdFromJwt).ToArray();

        SetAuth(tokens[0]);
        var resp = await _client.PostAsJsonAsync("/match/quick", new
        {
            sport      = "basket2v2",
            circleName = "Partitella Veloce",
            team1      = new[] { new { userId = ids[0] }, new { userId = ids[1] } },
            team2      = new[] { new { userId = ids[2] }, new { userId = ids[3] } },
            sets       = new[] { new { team1 = 21, team2 = 15 } },
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("circleCreated").GetBoolean());
        Assert.Equal("Partitella Veloce", body.GetProperty("circleName").GetString());

        var circleId = Guid.Parse(body.GetProperty("circleId").GetString()!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var circle = await db.Circles.FindAsync(circleId);
        Assert.NotNull(circle);
        Assert.True(circle!.IsPrivate);

        var memberCount = await db.CircleMemberships.CountAsync(m => m.CircleId == circleId);
        Assert.Equal(4, memberCount);

        var match = await db.Matches.FirstOrDefaultAsync(m => m.CircleId == circleId);
        Assert.NotNull(match);
    }

    [Fact]
    public async Task QuickMatch_ExistingCircle_UsesIt_NoNewCircle()
    {
        var (circleId, ids, tokens) = await SetupAsync("basket2v2");

        SetAuth(tokens[0]);
        var circlesBefore = await CountCirclesAsync();

        var resp = await PostQuickMatchAsync(circleId, "basket2v2", ids[0], ids[1], ids[2], ids[3]);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("circleCreated").GetBoolean());
        Assert.Equal(circleId.ToString(), body.GetProperty("circleId").GetString());

        var circlesAfter = await CountCirclesAsync();
        Assert.Equal(circlesBefore, circlesAfter);
    }

    [Fact]
    public async Task QuickMatch_ExistingCircle_WithNewGuest_AddsGhostToCircle()
    {
        var (circleId, ids, tokens) = await SetupAsync("basket2v2");

        SetAuth(tokens[0]);
        var guestEmail = $"ghost_{Guid.NewGuid():N}@test.com";

        var resp = await _client.PostAsJsonAsync("/match/quick", new
        {
            sport    = "basket2v2",
            circleId,
            team1    = new object[] { new { userId = ids[0] }, new { userId = ids[1] } },
            team2    = new object[] { new { userId = ids[2] }, new { guestName = "Ospite", guestEmail } },
            sets     = new[] { new { team1 = 21, team2 = 15 } },
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ghost = await db.Users.FirstOrDefaultAsync(u => u.Email == guestEmail);
        Assert.NotNull(ghost);
        Assert.False(ghost!.IsActivated);

        var membership = await db.CircleMemberships
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == ghost.Id);
        Assert.NotNull(membership);
    }

    [Fact]
    public async Task QuickMatch_WithGuestSlot_CreatesGhostAndMembership()
    {
        // New circle case: 3 users + 1 guest → ghost created, membership in new circle
        var tokens = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => RegisterTokenAsync()));
        var ids = tokens.Select(ExtractUserIdFromJwt).ToArray();

        SetAuth(tokens[0]);
        var guestEmail = $"guest_{Guid.NewGuid():N}@test.com";

        var resp = await _client.PostAsJsonAsync("/match/quick", new
        {
            sport  = "basket2v2",
            team1  = new object[] { new { userId = ids[0] }, new { userId = ids[1] } },
            team2  = new object[] { new { userId = ids[2] }, new { guestName = "Nuovo Ospite", guestEmail } },
            sets   = new[] { new { team1 = 21, team2 = 15 } },
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("circleCreated").GetBoolean());

        var circleId = Guid.Parse(body.GetProperty("circleId").GetString()!);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ghost = await db.Users.FirstOrDefaultAsync(u => u.Email == guestEmail);
        Assert.NotNull(ghost);
        Assert.False(ghost!.IsActivated);
        Assert.Empty(ghost.PasswordHash);

        var membership = await db.CircleMemberships
            .FirstOrDefaultAsync(m => m.CircleId == circleId && m.UserId == ghost.Id);
        Assert.NotNull(membership);
        Assert.Equal(1000, membership!.Rating);
    }

    [Fact]
    public async Task Check_ReturnsOwnerIdForEachCircle()
    {
        var (circleId, ids, tokens) = await SetupAsync("basket2v2");

        SetAuth(tokens[0]);
        var resp = await _client.PostAsJsonAsync("/match/quick/check", new
        {
            sport   = "basket2v2",
            userIds = ids,
            guests  = Array.Empty<object>(),
        });

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var circle = body.GetProperty("circles")[0];
        Assert.Equal(circleId, Guid.Parse(circle.GetProperty("id").GetString()!));
        Assert.Equal(ids[0], Guid.Parse(circle.GetProperty("ownerId").GetString()!));
    }

    // ─── US-071: owner registers without playing ───────────────────────────────

    [Fact]
    public async Task QuickMatch_OwnerNotPlaying_ExistingCircle_CreatesMatchWithoutOwnerConfirmation()
    {
        var (circleId, ids, tokens) = await SetupAsync("basket2v2");
        // owner = ids[0]/tokens[0]; register 4 other players not the owner
        var otherTokens = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegisterTokenAsync()));
        var otherIds = otherTokens.Select(ExtractUserIdFromJwt).ToArray();

        SetAuth(tokens[0]); // owner creates, not among the 4 players
        var resp = await _client.PostAsJsonAsync("/match/quick", new
        {
            sport   = "basket2v2",
            circleId,
            team1   = new object[] { new { userId = otherIds[0] }, new { userId = otherIds[1] } },
            team2   = new object[] { new { userId = otherIds[2] }, new { userId = otherIds[3] } },
            sets    = new[] { new { team1 = 21, team2 = 15 } },
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = Guid.Parse(body.GetProperty("matchId").GetString()!);
        Assert.Equal(4, body.GetProperty("confirmationLinks").GetArrayLength());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ownerId = ids[0];
        var ownerConfirmed = await db.MatchConfirmations
            .AnyAsync(c => c.MatchId == matchId && c.UserId == ownerId);
        Assert.False(ownerConfirmed);

        var tokenCount = await db.MatchConfirmationTokens.CountAsync(t => t.MatchId == matchId);
        Assert.Equal(4, tokenCount);
    }

    [Fact]
    public async Task QuickMatch_NonOwnerNotPlaying_ExistingCircle_Returns403()
    {
        var (circleId, ids, tokens) = await SetupAsync("basket2v2");
        // p2 (non-owner) tries to register a match without being one of the 4 players
        var otherTokens = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegisterTokenAsync()));
        var otherIds = otherTokens.Select(ExtractUserIdFromJwt).ToArray();

        SetAuth(tokens[1]); // non-owner member creates, not among the 4 players
        var resp = await _client.PostAsJsonAsync("/match/quick", new
        {
            sport   = "basket2v2",
            circleId,
            team1   = new object[] { new { userId = otherIds[0] }, new { userId = otherIds[1] } },
            team2   = new object[] { new { userId = otherIds[2] }, new { userId = otherIds[3] } },
            sets    = new[] { new { team1 = 21, team2 = 15 } },
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task QuickMatch_CreatorPlaying_ExistingCircle_ConfirmationUnchanged()
    {
        // Regression: creator among the 4 players, non-owner circle member → unchanged behaviour
        var (circleId, ids, tokens) = await SetupAsync("basket2v2");

        SetAuth(tokens[1]); // non-owner, but plays
        var resp = await PostQuickMatchAsync(circleId, "basket2v2", ids[1], ids[0], ids[2], ids[3]);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var matchId = Guid.Parse(body.GetProperty("matchId").GetString()!);
        Assert.Equal(3, body.GetProperty("confirmationLinks").GetArrayLength());

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var creatorConfirmed = await db.MatchConfirmations
            .AnyAsync(c => c.MatchId == matchId && c.UserId == ids[1]);
        Assert.True(creatorConfirmed);
    }

    [Fact]
    public async Task QuickMatch_OwnerNotPlaying_NewCircle_CreatesMatchWithoutOwnerConfirmation()
    {
        var creatorToken = await RegisterTokenAsync();
        var otherTokens = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegisterTokenAsync()));
        var otherIds = otherTokens.Select(ExtractUserIdFromJwt).ToArray();

        SetAuth(creatorToken); // creates new circle, becomes owner, but excludes self from players
        var resp = await _client.PostAsJsonAsync("/match/quick", new
        {
            sport      = "basket2v2",
            circleName = "Torneo del Circolo",
            team1      = new object[] { new { userId = otherIds[0] }, new { userId = otherIds[1] } },
            team2      = new object[] { new { userId = otherIds[2] }, new { userId = otherIds[3] } },
            sets       = new[] { new { team1 = 21, team2 = 15 } },
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("circleCreated").GetBoolean());
        Assert.Equal(4, body.GetProperty("confirmationLinks").GetArrayLength());

        var matchId = Guid.Parse(body.GetProperty("matchId").GetString()!);
        var creatorId = ExtractUserIdFromJwt(creatorToken);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var creatorConfirmed = await db.MatchConfirmations
            .AnyAsync(c => c.MatchId == matchId && c.UserId == creatorId);
        Assert.False(creatorConfirmed);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid CircleId, Guid[] Ids, string[] Tokens)> SetupAsync(string sport = "basket2v2")
    {
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
            [ownerId, p2Id, p3Id, p4Id],
            [ownerToken, p2Token, p3Token, p4Token]);
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
        var body = await r.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("token").GetString()!;
    }

    private Task<HttpResponseMessage> PostQuickMatchAsync(
        Guid? circleId, string sport, Guid t1p1, Guid t1p2, Guid t2p1, Guid t2p2)
    {
        var team1 = new[] { new { userId = t1p1 }, new { userId = t1p2 } };
        var team2 = new[] { new { userId = t2p1 }, new { userId = t2p2 } };
        var sets  = new[] { new { team1 = 21, team2 = 15 } };

        if (circleId.HasValue)
            return _client.PostAsJsonAsync("/match/quick", new { sport, circleId = circleId.Value, team1, team2, sets });
        return _client.PostAsJsonAsync("/match/quick", new { sport, team1, team2, sets });
    }

    private async Task<int> CountCirclesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Circles.CountAsync();
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

public class QuickMatchTestFactory : WebApplicationFactory<Program>
{
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
            var dbName = $"QuickMatchTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp)
                       .ConfigureWarnings(w => w.Ignore(
                           Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));

            services.RemoveAll(typeof(IPushNotificationService));
            services.AddSingleton<IPushNotificationService>(new SilentPushService());

            var emailCapture = new TestEmailCapture();
            services.AddSingleton(emailCapture);
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

public class SilentPushService : IPushNotificationService
{
    public Task SendConfirmationRequestAsync(Guid matchId, Guid circleId, Guid[] recipientUserIds) => Task.CompletedTask;
    public Task<bool> SendTestNotificationAsync(Guid userId) => Task.FromResult(false);
    public Task SendRankingImprovedAsync(Guid userId, int newPosition, string circleName) => Task.CompletedTask;
}
