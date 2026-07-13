using Golp.Api.Services;

namespace Golp.Tests.Services;

public class MatchmakingServiceTests
{
    private static Guid[] Players(int n) => Enumerable.Range(0, n).Select(_ => Guid.NewGuid()).ToArray();

    private static Dictionary<Guid, int> FlatScores(IEnumerable<Guid> ids, int score = 1000) =>
        ids.ToDictionary(id => id, _ => score);

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void SingleCourt_AnyPresentCount_RestingAreThoseExceedingCapacity(int presentCount)
    {
        var players = Players(presentCount);
        var scores = FlatScores(players);

        var plan = MatchmakingService.BuildPlan(players, scores, new Dictionary<(Guid, Guid), int>(), courts: 1, targetMode: "Total", targetValue: 1);

        var round = plan.Rounds.Single();
        Assert.True(round.Matches.Count <= 1);
        Assert.Equal(presentCount, round.Matches.Sum(m => m.Team1.Length + m.Team2.Length) + round.Resting.Count);
        Assert.Equal(Math.Max(0, presentCount - 4), round.Resting.Count);
    }

    [Fact]
    public void MultipleCourts_MatchesPerRound_NeverExceedCourts()
    {
        var players = Players(12);
        var scores = FlatScores(players);

        var plan = MatchmakingService.BuildPlan(players, scores, new Dictionary<(Guid, Guid), int>(), courts: 2, targetMode: "Total", targetValue: 6);

        Assert.All(plan.Rounds, r => Assert.True(r.Matches.Count <= 2));
    }

    [Fact]
    public void TargetMode_Total_ProducesCeilRoundsOverCourts()
    {
        var players = Players(8);
        var scores = FlatScores(players);

        var plan = MatchmakingService.BuildPlan(players, scores, new Dictionary<(Guid, Guid), int>(), courts: 2, targetMode: "Total", targetValue: 5);

        // ceil(5/2) = 3
        Assert.Equal(3, plan.Rounds.Count);
    }

    [Fact]
    public void TargetMode_PerPlayer_ProducesEnoughRoundsForEveryoneToReachTarget()
    {
        var players = Players(4);
        var scores = FlatScores(players);

        // 4 giocatori, 1 campo (capacità 4 a turno): target 3 partite a testa
        // => ceil(3*4 / (1*4)) = 3 turni, ognuno gioca tutti i turni (nessuno riposa mai con 4 su 1 campo)
        var plan = MatchmakingService.BuildPlan(players, scores, new Dictionary<(Guid, Guid), int>(), courts: 1, targetMode: "PerPlayer", targetValue: 3);

        Assert.Equal(3, plan.Rounds.Count);
        foreach (var p in players)
        {
            var gamesPlayed = plan.Rounds.Count(r => !r.Resting.Contains(p));
            Assert.Equal(3, gamesPlayed);
        }
    }

    [Fact]
    public void RestRotation_AcrossRounds_PrioritizesWhoRestedMost()
    {
        // 5 presenti, 1 campo: ogni turno 1 persona riposa. Su 5 turni ognuno deve riposare esattamente 1 volta.
        var players = Players(5);
        var scores = FlatScores(players);

        var plan = MatchmakingService.BuildPlan(players, scores, new Dictionary<(Guid, Guid), int>(), courts: 1, targetMode: "Total", targetValue: 5);

        var restCounts = players.ToDictionary(p => p, p => plan.Rounds.Count(r => r.Resting.Contains(p)));
        Assert.All(restCounts.Values, count => Assert.Equal(1, count));
    }

    [Fact]
    public void ScoreTieBreak_BalancesTeamsByCombinedScore()
    {
        var players = Players(4);
        var scores = new Dictionary<Guid, int>
        {
            [players[0]] = 1400,
            [players[1]] = 1300,
            [players[2]] = 1100,
            [players[3]] = 900,
        };

        var plan = MatchmakingService.BuildPlan(players, scores, new Dictionary<(Guid, Guid), int>(), courts: 1, targetMode: "Total", targetValue: 1);
        var match = plan.Rounds.Single().Matches.Single();

        // atteso: 1400+900 vs 1300+1100 (diff 100) è la combinazione più bilanciata tra i 3 possibili split
        var team1Sum = scores[match.Team1[0]] + scores[match.Team1[1]];
        var team2Sum = scores[match.Team2[0]] + scores[match.Team2[1]];
        Assert.Equal(100, Math.Abs(team1Sum - team2Sum));
    }

    [Fact]
    public void AlreadyPlayedPairs_AreAvoidedWhenAlternativeExists()
    {
        var players = Players(4);
        var scores = FlatScores(players);
        // players[0] e players[1] hanno già giocato insieme molte volte in stagione
        var heavyPairHistory = new Dictionary<(Guid, Guid), int>
        {
            [players[0].CompareTo(players[1]) <= 0 ? (players[0], players[1]) : (players[1], players[0])] = 10,
        };

        var plan = MatchmakingService.BuildPlan(players, scores, heavyPairHistory, courts: 1, targetMode: "Total", targetValue: 1);
        var match = plan.Rounds.Single().Matches.Single();

        var team1HasBothHeavy = match.Team1.Contains(players[0]) && match.Team1.Contains(players[1]);
        var team2HasBothHeavy = match.Team2.Contains(players[0]) && match.Team2.Contains(players[1]);
        Assert.False(team1HasBothHeavy || team2HasBothHeavy);
    }

    [Fact]
    public void GuestWithoutScoreEntry_TreatedAsNeutralDefault()
    {
        var players = Players(4);
        // nessuno score fornito per un "ospite" (players[3]) — il servizio userà un default neutro (1000)
        var scores = FlatScores(players.Take(3), score: 1000);

        var plan = MatchmakingService.BuildPlan(players, scores, new Dictionary<(Guid, Guid), int>(), courts: 1, targetMode: "Total", targetValue: 1);

        var round = plan.Rounds.Single();
        Assert.Single(round.Matches);
        var allAssigned = round.Matches[0].Team1.Concat(round.Matches[0].Team2);
        Assert.Contains(players[3], allAssigned);
    }

    [Fact]
    public void RoundsNeeded_IsClampedToMaxTwenty()
    {
        var players = Players(4);
        var scores = FlatScores(players);

        // PerPlayer con target enorme e 1 solo campo -> richiederebbe centinaia di turni, va tappato
        var plan = MatchmakingService.BuildPlan(players, scores, new Dictionary<(Guid, Guid), int>(), courts: 1, targetMode: "PerPlayer", targetValue: 100);

        Assert.Equal(20, plan.Rounds.Count);
    }

    [Fact]
    public void IncompleteLastGroup_IsSkippedNotForcedIntoAMatch()
    {
        // 6 presenti, 2 campi: capacità 8, ma solo 6 giocano -> 1 partita completa (4), 2 restano senza gruppo completo
        var players = Players(6);
        var scores = FlatScores(players);

        var plan = MatchmakingService.BuildPlan(players, scores, new Dictionary<(Guid, Guid), int>(), courts: 2, targetMode: "Total", targetValue: 1);
        var round = plan.Rounds.Single();

        Assert.Single(round.Matches);
        Assert.Empty(round.Resting); // tutti e 6 "giocano" lo slot (capacità 8 > 6), ma solo 4 vengono assegnati a un campo
    }
}
