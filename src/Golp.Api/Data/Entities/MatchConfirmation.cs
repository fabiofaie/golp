namespace Golp.Api.Data.Entities;

public class MatchConfirmation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; } = DateTimeOffset.UtcNow;

    public Match Match { get; set; } = null!;
    public User User { get; set; } = null!;
}
