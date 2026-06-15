namespace Golp.Api.Services;

public static class SportsConfig
{
    public record SportDto(string Sport, string PointUnit, bool Sets, int TeamSize, double SetWeight = 0.0);

    private static readonly IReadOnlyList<SportDto> All =
    [
        new("padel",       "games",  true,  2, 0.4),
        new("beachtennis", "games",  true,  2, 0.4),
        new("basket2v2",   "points", false, 2),
        new("burraco",     "score",  false, 2),
    ];

    public static IReadOnlyList<SportDto> GetAll() => All;

    public static SportDto? GetBySport(string sport) =>
        All.FirstOrDefault(s => s.Sport == sport);
}
