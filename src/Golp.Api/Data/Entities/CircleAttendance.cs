namespace Golp.Api.Data.Entities;

// US-049: check-in effimero per il matchmaking del raduno. Nessuna entità "raduno" persistita:
// la presenza è semplicemente "questo utente ha fatto check-in di recente in questo circolo" (TTL lato query).
public class CircleAttendance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CircleId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Circle Circle { get; set; } = null!;
    public User User { get; set; } = null!;
}
