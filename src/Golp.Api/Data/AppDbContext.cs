using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Circle> Circles => Set<Circle>();
    public DbSet<CircleMembership> CircleMemberships => Set<CircleMembership>();
    public DbSet<Match> Matches => Set<Match>();
    public DbSet<MatchSet> MatchSets => Set<MatchSet>();
    public DbSet<MatchConfirmation> MatchConfirmations => Set<MatchConfirmation>();
    public DbSet<FcmToken> FcmTokens => Set<FcmToken>();
    public DbSet<CircleAward> CircleAwards => Set<CircleAward>();
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<AwardNotificationSent> AwardNotificationsSent => Set<AwardNotificationSent>();
    public DbSet<MatchConfirmationToken> MatchConfirmationTokens => Set<MatchConfirmationToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Name).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(254);
            e.HasIndex(u => u.Email).IsUnique().HasFilter("[Email] IS NOT NULL");
            e.Property(u => u.Phone).HasMaxLength(30);
            e.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<PasswordResetToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            e.HasOne(t => t.User)
             .WithMany(u => u.PasswordResetTokens)
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
            e.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
            e.Property(t => t.UserAgent).HasMaxLength(512);
            e.HasIndex(t => t.TokenHash).IsUnique();
            e.HasIndex(t => t.FamilyId);
            e.HasOne(t => t.User)
             .WithMany()
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Circle>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).HasMaxLength(100).IsRequired();
            e.Property(c => c.Sport).HasMaxLength(50).IsRequired();
            e.Property(c => c.PointUnit).HasMaxLength(20).IsRequired();
            e.Property(c => c.JoinCode).HasMaxLength(32);
            e.HasIndex(c => new { c.OwnerId, c.Name }).IsUnique();
            e.HasOne(c => c.Owner)
             .WithMany()
             .HasForeignKey(c => c.OwnerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CircleMembership>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasIndex(m => new { m.CircleId, m.UserId }).IsUnique();
            e.HasOne(m => m.Circle)
             .WithMany(c => c.Members)
             .HasForeignKey(m => m.CircleId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(m => m.User)
             .WithMany()
             .HasForeignKey(m => m.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Match>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Status).HasMaxLength(20).IsRequired();
            e.HasIndex(m => new { m.CircleId, m.Status });
            e.HasOne(m => m.Circle)
             .WithMany()
             .HasForeignKey(m => m.CircleId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(m => m.CreatedBy)
             .WithMany()
             .HasForeignKey(m => m.CreatedById)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(m => m.Team1Player1Id).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(m => m.Team1Player2Id).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(m => m.Team2Player1Id).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<User>().WithMany().HasForeignKey(m => m.Team2Player2Id).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MatchSet>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasOne(s => s.Match)
             .WithMany(m => m.Sets)
             .HasForeignKey(s => s.MatchId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatchConfirmation>(e =>
        {
            e.HasKey(c => c.Id);
            e.HasIndex(c => new { c.MatchId, c.UserId }).IsUnique();
            e.HasOne(c => c.Match)
             .WithMany()
             .HasForeignKey(c => c.MatchId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.User)
             .WithMany()
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FcmToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Token).HasMaxLength(512).IsRequired();
            e.Property(t => t.DeviceId).HasMaxLength(100).IsRequired();
            e.HasIndex(t => new { t.UserId, t.Token }).IsUnique();
            e.HasOne(t => t.User)
             .WithMany()
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CircleAward>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.PeriodType).HasMaxLength(10).IsRequired();
            e.HasIndex(a => new { a.CircleId, a.PeriodType, a.PeriodYear, a.PeriodMonth }).IsUnique();
        });

        modelBuilder.Entity<AwardNotificationSent>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.PeriodType).HasMaxLength(10).IsRequired();
            e.Property(a => a.PeriodLabel).HasMaxLength(20).IsRequired();
            e.HasIndex(a => new { a.CircleId, a.PeriodType, a.PeriodLabel }).IsUnique();
        });

        modelBuilder.Entity<MatchConfirmationToken>(e =>
        {
            e.HasKey(t => t.Id);
            e.HasIndex(t => new { t.MatchId, t.UserId }).IsUnique();
            e.HasIndex(t => t.Token).IsUnique();
            e.HasOne(t => t.Match)
             .WithMany()
             .HasForeignKey(t => t.MatchId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.User)
             .WithMany()
             .HasForeignKey(t => t.UserId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Sport>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Key).HasMaxLength(50).IsRequired();
            e.Property(s => s.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(s => s.PointUnit).HasMaxLength(20).IsRequired();
            e.HasIndex(s => s.Key).IsUnique();

            e.HasData(
                new Sport { Id = 1, Key = "padel", DisplayName = "Padel", PointUnit = "games", Sets = true, TeamSize = 2, IsActive = true, SetWeight = 0.4 },
                new Sport { Id = 2, Key = "beachtennis", DisplayName = "Beach Tennis", PointUnit = "games", Sets = true, TeamSize = 2, IsActive = true, SetWeight = 0.4 },
                new Sport { Id = 3, Key = "basket2v2", DisplayName = "Basket 2v2", PointUnit = "points", Sets = false, TeamSize = 2, IsActive = true, SetWeight = 0.0 },
                new Sport { Id = 4, Key = "burraco", DisplayName = "Burraco", PointUnit = "score", Sets = false, TeamSize = 2, IsActive = true, SetWeight = 0.0 }
            );
        });
    }
}
