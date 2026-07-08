using Golp.Api.Services;

namespace Golp.Api.Endpoints;

public static class SimulateGameBonusEndpoints
{
    public static IEndpointRouteBuilder MapSimulateGameBonusEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/simulate-game-bonus", SimulateAsync);
        return app;
    }

    private static IResult SimulateAsync(SimulateGameBonusRequest req)
    {
        if (req.Sets is null || req.Sets.Count == 0)
            return Results.BadRequest("sets è obbligatorio.");

        if (req.Sets.Any(s => s.Team1Score < 0 || s.Team2Score < 0))
            return Results.BadRequest("I punteggi non possono essere negativi.");

        if (req.Team1CurrentScore < 0 || req.Team2CurrentScore < 0)
            return Results.BadRequest("I punteggi Game+Bonus correnti non possono essere negativi.");

        int totalTeam1 = req.Sets.Sum(s => s.Team1Score);
        int totalTeam2 = req.Sets.Sum(s => s.Team2Score);

        if (totalTeam1 + totalTeam2 == 0)
            return Results.BadRequest("Il totale dei punteggi deve essere maggiore di zero.");

        // Vincitore determinato dai set vinti (maggioranza), non dalla somma game totale (US-056).
        // Per un singolo elemento (risultato unico / sport senza set) collassa su "chi ha più game".
        int setsWonByTeam1 = req.Sets.Count(s => s.Team1Score > s.Team2Score);
        int setsWonByTeam2 = req.Sets.Count(s => s.Team2Score > s.Team1Score);
        if (setsWonByTeam1 == setsWonByTeam2)
            return Results.BadRequest("Impossibile determinare la squadra vincente: set vinti pari.");

        bool team1Won = setsWonByTeam1 > setsWonByTeam2;
        var sets = req.Sets.Select(s => (s.Team1Score, s.Team2Score)).ToList();
        double winnerScore = team1Won ? req.Team1CurrentScore : req.Team2CurrentScore;
        double loserScore  = team1Won ? req.Team2CurrentScore : req.Team1CurrentScore;

        int winnerPoints = GameBonusRatingService.ComputeMatchPoints(sets, team1Won, winnerScore, loserScore);

        int team1Points = team1Won ? winnerPoints : 0;
        int team2Points = team1Won ? 0 : winnerPoints;

        return Results.Ok(new SimulateGameBonusResponse(team1Points, team2Points));
    }
}

record SimulateGameBonusSet(int Team1Score, int Team2Score);

record SimulateGameBonusRequest(
    List<SimulateGameBonusSet>? Sets,
    double Team1CurrentScore = 0,
    double Team2CurrentScore = 0);

record SimulateGameBonusResponse(int Team1Points, int Team2Points);
