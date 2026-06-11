namespace Golp.Api.Data.Entities;

public class CircleMembership
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CircleId { get; set; }
    public Guid UserId { get; set; }
    public int Rating { get; set; } = 1000;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Circle Circle { get; set; } = null!;
    public User User { get; set; } = null!;
}
