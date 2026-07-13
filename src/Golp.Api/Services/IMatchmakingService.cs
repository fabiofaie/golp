using Golp.Api.Data;

namespace Golp.Api.Services;

public record PlannedMatch(Guid[] Team1, Guid[] Team2);

public record PlannedRound(int Index, List<PlannedMatch> Matches, List<Guid> Resting);

public record MatchmakingPlan(List<PlannedRound> Rounds);

public interface IMatchmakingService
{
    /// <summary>
    /// US-049: pianifica N turni di accoppiamenti per i membri attualmente presenti nel circolo
    /// (v. <see cref="CircleAttendance"/>), rispettando <paramref name="courts"/> partite in parallelo
    /// per turno e l'obiettivo di partite (<paramref name="targetMode"/> "Total" o "PerPlayer",
    /// <paramref name="targetValue"/>). Nessuna scrittura sul DB: output puramente derivato,
    /// rigenerabile ad ogni chiamata.
    /// </summary>
    Task<MatchmakingPlan> BuildPlanAsync(
        Guid circleId, IReadOnlyList<Guid> presentUserIds, int courts, string targetMode, int targetValue, AppDbContext db);
}
