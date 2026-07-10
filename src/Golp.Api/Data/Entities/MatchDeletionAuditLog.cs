namespace Golp.Api.Data.Entities;

public class MatchDeletionAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SuperAdminId { get; set; }
    public Guid CircleId { get; set; }
    public Guid MatchId { get; set; }
    public string MatchSnapshotJson { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
}
