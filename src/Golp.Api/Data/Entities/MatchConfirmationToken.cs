namespace Golp.Api.Data.Entities;

public class MatchConfirmationToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public Guid UserId { get; set; }
    public Guid Token { get; set; } = Guid.NewGuid();
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }

    public Match Match { get; set; } = null!;
    public User User { get; set; } = null!;
}
