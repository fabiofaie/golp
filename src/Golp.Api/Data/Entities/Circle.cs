namespace Golp.Api.Data.Entities;

public class Circle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sport { get; set; } = string.Empty;
    public string PointUnit { get; set; } = string.Empty;
    public bool Sets { get; set; }
    public int TeamSize { get; set; } = 2;
    public bool IsPrivate { get; set; }
    public string? JoinCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public ICollection<CircleMembership> Members { get; set; } = [];
}
