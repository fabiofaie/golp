namespace Golp.Api.Services;

public interface IAwardNotificationProcessor
{
    Task ProcessAsync(DateTimeOffset now, CancellationToken ct = default);
}
