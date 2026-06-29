namespace Golp.Api.Services;

public class AwardNotificationBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AwardNotificationBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AwardNotificationBackgroundService avviato");

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = ComputeDelayToNextRun();
            logger.LogInformation("Prossima verifica premi tra {Minutes} minuti", delay.TotalMinutes);

            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) { break; }

            await RunAsync(stoppingToken);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<IAwardNotificationProcessor>();
            await processor.ProcessAsync(DateTimeOffset.UtcNow, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Errore nel background job notifiche premi");
        }
    }

    private TimeSpan ComputeDelayToNextRun()
    {
        var targetHour = configuration.GetValue("Awards:NotificationCheckHourUtc", 3);
        var now = DateTimeOffset.UtcNow;
        var nextRun = new DateTimeOffset(now.Year, now.Month, now.Day, targetHour, 0, 0, TimeSpan.Zero);
        if (nextRun <= now)
            nextRun = nextRun.AddDays(1);

        return nextRun - now;
    }
}
