namespace Golp.Api.Data.Entities;

public class MatchResultEditAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SuperAdminId { get; set; }
    public Guid CircleId { get; set; }
    public Guid MatchId { get; set; }
    public string PreviousResultJson { get; set; } = string.Empty;
    public string NewResultJson { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
}
