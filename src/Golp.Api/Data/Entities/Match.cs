namespace Golp.Api.Data.Entities;

public class Match
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CircleId { get; set; }
    public Guid CreatedById { get; set; }
    public string Status { get; set; } = "pending";
    public int WinnerTeam { get; set; }
    public bool IsSingles { get; set; }
    public Guid Team1Player1Id { get; set; }
    public Guid? Team1Player2Id { get; set; }
    public Guid Team2Player1Id { get; set; }
    public Guid? Team2Player2Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Delta ELO per giocatore (US-007). Null = non ancora confermata o disputed.
    public int? DeltaTeam1Player1 { get; set; }
    public int? DeltaTeam1Player2 { get; set; }
    public int? DeltaTeam2Player1 { get; set; }
    public int? DeltaTeam2Player2 { get; set; }

    // US-052: punti Game+Bonus assegnati alla squadra vincente (null = non applicabile/non confermato con questo metodo)
    public int? GameBonusWinnerPoints { get; set; }

    public Guid? ForceConfirmedById { get; set; }
    public DateTimeOffset? ForceConfirmedAt { get; set; }

    public Circle Circle { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<MatchSet> Sets { get; set; } = [];
}
