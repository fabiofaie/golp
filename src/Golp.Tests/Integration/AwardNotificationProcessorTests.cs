using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Golp.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Golp.Tests.Integration;

public class AwardNotificationProcessorTests : IClassFixture<AwardNotificationTestFactory>
{
    private readonly AwardNotificationTestFactory _factory;

    public AwardNotificationProcessorTests(AwardNotificationTestFactory factory)
    {
        _factory = factory;
    }

    // AC2 + AC3: circolo con partita confermata nel periodo → email inviata, riga registrata
    [Fact]
    public async Task ProcessAsync_CircleWithWinner_SendsEmailAndRegisters()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailMock = _factory.EmailMock;
        emailMock.Invocations.Clear();

        var (circleId, _, userId, _) = await SeedCircleWithMatchAsync(scope, prevMonthOf: TestNow);

        var processor = scope.ServiceProvider.GetRequiredService<IAwardNotificationProcessor>();
        await processor.ProcessAsync(TestNow);

        var monthLabel = $"{TestNow.AddMonths(-1).Year}-{TestNow.AddMonths(-1).Month:D2}";
        var sent = await db.AwardNotificationsSent
            .AnyAsync(a => a.CircleId == circleId && a.PeriodType == "month" && a.PeriodLabel == monthLabel);

        Assert.True(sent);
        emailMock.Verify(e => e.SendAwardWinnerEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.AtLeastOnce);
    }

    // AC5: idempotenza — secondo run sullo stesso periodo non invia nuova email
    [Fact]
    public async Task ProcessAsync_SamePeriodTwice_NoDuplicateEmail()
    {
        using var scope = _factory.Services.CreateScope();
        var emailMock = _factory.EmailMock;
        emailMock.Invocations.Clear();

        await SeedCircleWithMatchAsync(scope, prevMonthOf: TestNow);

        var processor = scope.ServiceProvider.GetRequiredService<IAwardNotificationProcessor>();
        await processor.ProcessAsync(TestNow);
        emailMock.Invocations.Clear();

        // Second run
        await processor.ProcessAsync(TestNow);

        emailMock.Verify(e => e.SendAwardWinnerEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // AC4: circolo senza partite confermate → nessuna email, ma periodo registrato
    [Fact]
    public async Task ProcessAsync_NoConfirmedMatches_NoEmailButPeriodRegistered()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailMock = _factory.EmailMock;
        emailMock.Invocations.Clear();

        var circleId = await SeedEmptyCircleAsync(scope);

        var processor = scope.ServiceProvider.GetRequiredService<IAwardNotificationProcessor>();
        await processor.ProcessAsync(TestNow);

        var monthLabel = $"{TestNow.AddMonths(-1).Year}-{TestNow.AddMonths(-1).Month:D2}";
        var registered = await db.AwardNotificationsSent
            .AnyAsync(a => a.CircleId == circleId && a.PeriodType == "month" && a.PeriodLabel == monthLabel);

        Assert.True(registered);
        emailMock.Verify(e => e.SendAwardWinnerEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    // AC6: email fallisce per un circolo → altri circoli processati comunque
    [Fact]
    public async Task ProcessAsync_EmailFailsForOneCircle_OthersStillProcessed()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailMock = _factory.EmailMock;
        emailMock.Invocations.Clear();

        var (circle1Id, _, _, _) = await SeedCircleWithMatchAsync(scope, prevMonthOf: TestNow);
        var (circle2Id, _, _, _) = await SeedCircleWithMatchAsync(scope, prevMonthOf: TestNow);

        // First call throws, second succeeds
        emailMock
            .SetupSequence(e => e.SendAwardWinnerEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .ThrowsAsync(new Exception("SMTP error"))
            .Returns(Task.CompletedTask);

        var processor = scope.ServiceProvider.GetRequiredService<IAwardNotificationProcessor>();
        await processor.ProcessAsync(TestNow);

        var monthLabel = $"{TestNow.AddMonths(-1).Year}-{TestNow.AddMonths(-1).Month:D2}";
        var circle1Done = await db.AwardNotificationsSent
            .AnyAsync(a => a.CircleId == circle1Id && a.PeriodType == "month" && a.PeriodLabel == monthLabel);
        var circle2Done = await db.AwardNotificationsSent
            .AnyAsync(a => a.CircleId == circle2Id && a.PeriodType == "month" && a.PeriodLabel == monthLabel);

        Assert.True(circle1Done);
        Assert.True(circle2Done);

        // Restore default mock behavior for other tests
        emailMock.Setup(e => e.SendAwardWinnerEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
    }

    // ─── helpers ─────────────────────────────────────────────────────────────

    // Fixed "now" for all tests: first day of a month so prev month is unambiguous
    private static readonly DateTimeOffset TestNow = new(2026, 7, 1, 4, 0, 0, TimeSpan.Zero);

    private static async Task<(Guid CircleId, Guid OwnerId, Guid WinnerId, string WinnerEmail)>
        SeedCircleWithMatchAsync(IServiceScope scope, DateTimeOffset prevMonthOf)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var owner = new User { Name = $"Owner_{Guid.NewGuid():N}", Email = $"o_{Guid.NewGuid():N}@t.com", PasswordHash = "x" };
        var player2 = new User { Name = $"P2_{Guid.NewGuid():N}", Email = $"p2_{Guid.NewGuid():N}@t.com", PasswordHash = "x" };
        var player3 = new User { Name = $"P3_{Guid.NewGuid():N}", Email = $"p3_{Guid.NewGuid():N}@t.com", PasswordHash = "x" };
        var player4 = new User { Name = $"P4_{Guid.NewGuid():N}", Email = $"p4_{Guid.NewGuid():N}@t.com", PasswordHash = "x" };
        db.Users.AddRange(owner, player2, player3, player4);

        var circle = new Circle { Name = $"C_{Guid.NewGuid():N}", Sport = "padel", PointUnit = "games", OwnerId = owner.Id };
        db.Circles.Add(circle);

        var prev = prevMonthOf.AddMonths(-1);
        var matchDate = new DateTimeOffset(prev.Year, prev.Month, 15, 0, 0, 0, TimeSpan.Zero);

        db.Matches.Add(new Golp.Api.Data.Entities.Match
        {
            CircleId          = circle.Id,
            CreatedById       = owner.Id,
            Status            = "confirmed",
            WinnerTeam        = 1,
            Team1Player1Id    = owner.Id,
            Team1Player2Id    = player2.Id,
            Team2Player1Id    = player3.Id,
            Team2Player2Id    = player4.Id,
            CreatedAt         = matchDate,
            DeltaTeam1Player1 = 30,
            DeltaTeam1Player2 = 20,
            DeltaTeam2Player1 = -15,
            DeltaTeam2Player2 = -15,
        });

        await db.SaveChangesAsync();
        return (circle.Id, owner.Id, owner.Id, owner.Email);
    }

    private static async Task<Guid> SeedEmptyCircleAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var owner = new User { Name = $"OE_{Guid.NewGuid():N}", Email = $"oe_{Guid.NewGuid():N}@t.com", PasswordHash = "x" };
        db.Users.Add(owner);
        var circle = new Circle { Name = $"CE_{Guid.NewGuid():N}", Sport = "padel", PointUnit = "games", OwnerId = owner.Id };
        db.Circles.Add(circle);
        await db.SaveChangesAsync();
        return circle.Id;
    }
}

// ─── Factory ─────────────────────────────────────────────────────────────────

public class AwardNotificationTestFactory : WebApplicationFactory<Program>
{
    public Mock<IEmailService> EmailMock { get; } = new();

    public AwardNotificationTestFactory()
    {
        EmailMock
            .Setup(e => e.SendAwardWinnerEmailAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace DB with in-memory
            var toRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                         || d.ServiceType == typeof(AppDbContext)
                         || d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true)
                .ToList();
            foreach (var d in toRemove) services.Remove(d);

            services.AddEntityFrameworkInMemoryDatabase();
            var dbName = $"AwardNotifTestDb_{Guid.NewGuid()}";
            services.AddDbContext<AppDbContext>((sp, options) =>
                options.UseInMemoryDatabase(dbName)
                       .UseInternalServiceProvider(sp));

            // Replace email service with mock
            var emailDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null) services.Remove(emailDescriptor);
            services.AddScoped<IEmailService>(_ => EmailMock.Object);

            // Remove background service (don't let it run during tests)
            var bgDescriptor = services.FirstOrDefault(
                d => d.ImplementationType == typeof(AwardNotificationBackgroundService));
            if (bgDescriptor != null) services.Remove(bgDescriptor);

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
