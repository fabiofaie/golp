namespace Golp.Api.Data.Entities;

public class ImpersonationAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SuperAdminId { get; set; }
    public Guid TargetUserId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAt { get; set; }
}
