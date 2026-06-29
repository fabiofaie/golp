namespace Golp.Api.Data.Entities;

public class AwardNotificationSent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CircleId { get; set; }
    public string PeriodType { get; set; } = "";    // "month" | "year"
    public string PeriodLabel { get; set; } = "";   // "2026-06" | "2026"
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool EmailSent { get; set; }
}
