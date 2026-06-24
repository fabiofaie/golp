namespace Golp.Api.Services;

public interface ISportsService
{
    Task<IReadOnlyList<SportDto>> GetAllAsync();
    Task<SportDto?> GetBySportAsync(string sport);
}

public record SportDto(string Sport, string DisplayName, string PointUnit, bool Sets, int TeamSize, double SetWeight = 0.0);
