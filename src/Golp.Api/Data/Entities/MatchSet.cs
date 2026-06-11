namespace Golp.Api.Data.Entities;

public class MatchSet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MatchId { get; set; }
    public int SetNumber { get; set; }
    public int Team1Score { get; set; }
    public int Team2Score { get; set; }

    public Match Match { get; set; } = null!;
}
