namespace Golp.Api.Data.Entities;

public class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CircleId { get; set; }
    public Guid CreatedById { get; set; }
    public string Status { get; set; } = "pending";
    public int WinnerTeam { get; set; }
    public Guid Team1Player1Id { get; set; }
    public Guid Team1Player2Id { get; set; }
    public Guid Team2Player1Id { get; set; }
    public Guid Team2Player2Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Circle Circle { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<MatchSet> Sets { get; set; } = [];
}
