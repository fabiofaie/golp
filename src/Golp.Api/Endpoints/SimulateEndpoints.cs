using Golp.Api.Services;

namespace Golp.Api.Endpoints;

public static class SimulateEndpoints
{
    public static IEndpointRouteBuilder MapSimulateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/simulate-match", SimulateAsync);
        return app;
    }

    private static IResult SimulateAsync(SimulateMatchRequest req)
    {
        if (req.Team1 is null || req.Team2 is null || req.Sets is null || req.Sets.Count == 0)
            return Results.BadRequest("team1, team2 e sets sono obbligatori.");

        if (req.Sets.Any(s => s.Team1Score < 0 || s.Team2Score < 0))
            return Results.BadRequest("I punteggi non possono essere negativi.");

        int totalTeam1 = req.Sets.Sum(s => s.Team1Score);
        int totalTeam2 = req.Sets.Sum(s => s.Team2Score);
        int total = totalTeam1 + totalTeam2;

        if (total == 0)
            return Results.BadRequest("Il totale dei punteggi deve essere maggiore di zero.");

        if (req.Team1.Player1Rating < 0 || req.Team1.Player1Rating > 3000 ||
            req.Team1.Player2Rating < 0 || req.Team1.Player2Rating > 3000 ||
            req.Team2.Player1Rating < 0 || req.Team2.Player1Rating > 3000 ||
            req.Team2.Player2Rating < 0 || req.Team2.Player2Rating > 3000)
            return Results.BadRequest("I rating devono essere compresi tra 0 e 3000.");

        double team1Avg = (req.Team1.Player1Rating + req.Team1.Player2Rating) / 2.0;
        double team2Avg = (req.Team2.Player1Rating + req.Team2.Player2Rating) / 2.0;

        bool team1Won = totalTeam1 >= totalTeam2;
        int winnerTotal = team1Won ? totalTeam1 : totalTeam2;
        double scoreRatio = Math.Clamp((double)winnerTotal / total, 0.5, 1.0);

        int k = req.Experienced ? RatingService.KDefault : RatingService.KColdStart;
        var kValues = new[] { k, k, k, k };

        var deltas = RatingService.ComputeDeltas(team1Avg, team2Avg, kValues, team1Won, scoreRatio);

        return Results.Ok(new SimulateMatchResponse(deltas[0], deltas[1], deltas[2], deltas[3]));
    }
}

record SimulateTeam(double Player1Rating, double Player2Rating);
record SimulateSet(int Team1Score, int Team2Score);

record SimulateMatchRequest(
    SimulateTeam? Team1,
    SimulateTeam? Team2,
    List<SimulateSet>? Sets,
    bool Experienced = true);

record SimulateMatchResponse(
    int DeltaTeam1Player1,
    int DeltaTeam1Player2,
    int DeltaTeam2Player1,
    int DeltaTeam2Player2);
