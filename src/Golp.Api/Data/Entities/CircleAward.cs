namespace Golp.Api.Data.Entities;

public class CircleAward
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CircleId { get; set; }
    public Guid? WinnerUserId { get; set; }
    public string PeriodType { get; set; } = "";   // "month" | "year"
    public int PeriodYear { get; set; }
    public int? PeriodMonth { get; set; }           // null per PeriodType="year"
    public int? TotalDelta { get; set; }
    public DateTimeOffset ComputedAt { get; set; } = DateTimeOffset.UtcNow;
}
