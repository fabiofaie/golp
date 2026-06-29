namespace Golp.Api.Services;

public record AwardWinner(Guid UserId, string Name, int NetGain, int MatchesPlayed);
public record AwardPeriodResult(string Period, AwardWinner? Winner);

public interface IAwardsCalculator
{
    Task<AwardPeriodResult> ComputePeriodAsync(Guid circleId, string periodType, int year, int? month);
}
