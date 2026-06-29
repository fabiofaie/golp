using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Golp.Tests.Services;

public class PushNotificationServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PushTestDb_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    private static PushNotificationService CreateService(AppDbContext db, Mock<IFcmSender> fcmMock) =>
        new(db, fcmMock.Object, NullLogger<PushNotificationService>.Instance);

    private static User CreateUser() => new()
    {
        Name = "Player",
        Email = $"u_{Guid.NewGuid():N}@test.com",
        PasswordHash = "hash"
    };

    [Fact]
    public async Task SendConfirmationRequest_NoTokensRegistered_DoesNotCallFcm()
    {
        using var db = CreateDb();
        var fcm = new Mock<IFcmSender>();
        var service = CreateService(db, fcm);

        await service.SendConfirmationRequestAsync(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid()]);

        fcm.Verify(f => f.SendAsync(
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
    }

    [Fact]
    public async Task SendConfirmationRequest_PayloadContainsMatchIdAndCircleId()
    {
        using var db = CreateDb();
        var user = CreateUser();
        db.Users.Add(user);
        db.FcmTokens.Add(new FcmToken { UserId = user.Id, Token = "tok-1", DeviceId = "dev-1" });
        await db.SaveChangesAsync();

        var matchId = Guid.NewGuid();
        var circleId = Guid.NewGuid();
        IReadOnlyDictionary<string, string>? sentData = null;
        IReadOnlyList<string>? sentTokens = null;

        var fcm = new Mock<IFcmSender>();
        fcm.Setup(f => f.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<IReadOnlyList<string>, string, string, IReadOnlyDictionary<string, string>>(
                (tokens, _, _, data) => { sentTokens = tokens; sentData = data; })
            .ReturnsAsync([new FcmSendResult("tok-1", true, false)]);

        var service = CreateService(db, fcm);
        await service.SendConfirmationRequestAsync(matchId, circleId, [user.Id]);

        Assert.NotNull(sentData);
        Assert.Equal(matchId.ToString(), sentData!["matchId"]);
        Assert.Equal(circleId.ToString(), sentData["circleId"]);
        Assert.Equal(["tok-1"], sentTokens);
    }

    [Fact]
    public async Task SendConfirmationRequest_OnlyRecipientTokensAreSent()
    {
        using var db = CreateDb();
        var recipient = CreateUser();
        var other = CreateUser();
        db.Users.AddRange(recipient, other);
        db.FcmTokens.AddRange(
            new FcmToken { UserId = recipient.Id, Token = "tok-recipient", DeviceId = "d1" },
            new FcmToken { UserId = other.Id, Token = "tok-other", DeviceId = "d2" });
        await db.SaveChangesAsync();

        IReadOnlyList<string>? sentTokens = null;
        var fcm = new Mock<IFcmSender>();
        fcm.Setup(f => f.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<IReadOnlyList<string>, string, string, IReadOnlyDictionary<string, string>>(
                (tokens, _, _, _) => sentTokens = tokens)
            .ReturnsAsync([new FcmSendResult("tok-recipient", true, false)]);

        var service = CreateService(db, fcm);
        await service.SendConfirmationRequestAsync(Guid.NewGuid(), Guid.NewGuid(), [recipient.Id]);

        Assert.Equal(["tok-recipient"], sentTokens);
    }

    [Fact]
    public async Task SendConfirmationRequest_UnregisteredToken_IsRemovedFromDb()
    {
        using var db = CreateDb();
        var user = CreateUser();
        db.Users.Add(user);
        db.FcmTokens.AddRange(
            new FcmToken { UserId = user.Id, Token = "tok-dead", DeviceId = "d1" },
            new FcmToken { UserId = user.Id, Token = "tok-alive", DeviceId = "d2" });
        await db.SaveChangesAsync();

        var fcm = new Mock<IFcmSender>();
        fcm.Setup(f => f.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync([
                new FcmSendResult("tok-dead", false, true),
                new FcmSendResult("tok-alive", true, false)
            ]);

        var service = CreateService(db, fcm);
        await service.SendConfirmationRequestAsync(Guid.NewGuid(), Guid.NewGuid(), [user.Id]);

        var remaining = await db.FcmTokens.Select(t => t.Token).ToListAsync();
        Assert.Equal(["tok-alive"], remaining);
    }

    [Fact]
    public async Task SendConfirmationRequest_FcmThrows_DoesNotPropagate()
    {
        using var db = CreateDb();
        var user = CreateUser();
        db.Users.Add(user);
        db.FcmTokens.Add(new FcmToken { UserId = user.Id, Token = "tok-1", DeviceId = "d1" });
        await db.SaveChangesAsync();

        var fcm = new Mock<IFcmSender>();
        fcm.Setup(f => f.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ThrowsAsync(new InvalidOperationException("FCM unreachable"));

        var service = CreateService(db, fcm);

        // Non deve lanciare: fire-and-forget, partita già persistita
        await service.SendConfirmationRequestAsync(Guid.NewGuid(), Guid.NewGuid(), [user.Id]);
    }

    // US-035 — SendRankingImprovedAsync

    [Fact]
    public async Task SendRankingImproved_NoToken_DoesNotCallFcm()
    {
        using var db = CreateDb();
        var fcm = new Mock<IFcmSender>();
        var service = CreateService(db, fcm);

        await service.SendRankingImprovedAsync(Guid.NewGuid(), 2, "TestCircle");

        fcm.Verify(f => f.SendAsync(
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
    }

    [Fact]
    public async Task SendRankingImproved_TokenPresent_SendsWithCorrectBody()
    {
        using var db = CreateDb();
        var user = CreateUser();
        db.Users.Add(user);
        db.FcmTokens.Add(new FcmToken { UserId = user.Id, Token = "tok-rank", DeviceId = "d1" });
        await db.SaveChangesAsync();

        string? sentBody = null;
        var fcm = new Mock<IFcmSender>();
        fcm.Setup(f => f.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<IReadOnlyList<string>, string, string, IReadOnlyDictionary<string, string>>(
                (_, _, body, _) => sentBody = body)
            .ReturnsAsync([new FcmSendResult("tok-rank", true, false)]);

        var service = CreateService(db, fcm);
        await service.SendRankingImprovedAsync(user.Id, 3, "CircleX");

        Assert.NotNull(sentBody);
        Assert.Contains("3°", sentBody);
        Assert.Contains("CircleX", sentBody);
    }

    [Fact]
    public async Task SendRankingImproved_UnregisteredToken_IsRemoved()
    {
        using var db = CreateDb();
        var user = CreateUser();
        db.Users.Add(user);
        db.FcmTokens.Add(new FcmToken { UserId = user.Id, Token = "tok-dead", DeviceId = "d1" });
        await db.SaveChangesAsync();

        var fcm = new Mock<IFcmSender>();
        fcm.Setup(f => f.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync([new FcmSendResult("tok-dead", false, true)]);

        var service = CreateService(db, fcm);
        await service.SendRankingImprovedAsync(user.Id, 1, "C");

        Assert.Empty(await db.FcmTokens.ToListAsync());
    }

    [Fact]
    public async Task SendRankingImproved_FcmThrows_DoesNotPropagate()
    {
        using var db = CreateDb();
        var user = CreateUser();
        db.Users.Add(user);
        db.FcmTokens.Add(new FcmToken { UserId = user.Id, Token = "tok-1", DeviceId = "d1" });
        await db.SaveChangesAsync();

        var fcm = new Mock<IFcmSender>();
        fcm.Setup(f => f.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ThrowsAsync(new InvalidOperationException("FCM down"));

        var service = CreateService(db, fcm);
        await service.SendRankingImprovedAsync(user.Id, 2, "C"); // must not throw
    }

    [Fact]
    public async Task SendTestNotification_NoTokensRegistered_ReturnsFalseAndDoesNotCallFcm()
    {
        using var db = CreateDb();
        var fcm = new Mock<IFcmSender>();
        var service = CreateService(db, fcm);

        var result = await service.SendTestNotificationAsync(Guid.NewGuid());

        Assert.False(result);
        fcm.Verify(f => f.SendAsync(
            It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
    }

    [Fact]
    public async Task SendTestNotification_TokenRegistered_ReturnsTrueAndSendsToUserTokensOnly()
    {
        using var db = CreateDb();
        var user = CreateUser();
        var other = CreateUser();
        db.Users.AddRange(user, other);
        db.FcmTokens.AddRange(
            new FcmToken { UserId = user.Id, Token = "tok-mine", DeviceId = "d1" },
            new FcmToken { UserId = other.Id, Token = "tok-other", DeviceId = "d2" });
        await db.SaveChangesAsync();

        IReadOnlyList<string>? sentTokens = null;
        var fcm = new Mock<IFcmSender>();
        fcm.Setup(f => f.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .Callback<IReadOnlyList<string>, string, string, IReadOnlyDictionary<string, string>>(
                (tokens, _, _, _) => sentTokens = tokens)
            .ReturnsAsync([new FcmSendResult("tok-mine", true, false)]);

        var service = CreateService(db, fcm);
        var result = await service.SendTestNotificationAsync(user.Id);

        Assert.True(result);
        Assert.Equal(["tok-mine"], sentTokens);
    }

    [Fact]
    public async Task SendTestNotification_FcmThrows_ReturnsFalseInsteadOfPropagating()
    {
        using var db = CreateDb();
        var user = CreateUser();
        db.Users.Add(user);
        db.FcmTokens.Add(new FcmToken { UserId = user.Id, Token = "tok-1", DeviceId = "d1" });
        await db.SaveChangesAsync();

        var fcm = new Mock<IFcmSender>();
        fcm.Setup(f => f.SendAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ThrowsAsync(new InvalidOperationException("FCM unreachable"));

        var service = CreateService(db, fcm);
        var result = await service.SendTestNotificationAsync(user.Id);

        Assert.False(result);
    }
}
