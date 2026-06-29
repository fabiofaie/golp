using Golp.Api.Data;
using Golp.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

public class AwardNotificationProcessor(
    AppDbContext db,
    IAwardsCalculator calculator,
    IEmailService emailService,
    ILogger<AwardNotificationProcessor> logger) : IAwardNotificationProcessor
{
    public async Task ProcessAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var circles = await db.Circles.Select(c => new { c.Id, c.Name }).ToListAsync(ct);

        // Previous month
        var prevMonth = now.AddMonths(-1);
        var monthLabel = $"{prevMonth.Year}-{prevMonth.Month:D2}";

        // Previous year
        var yearLabel = $"{now.Year - 1}";

        foreach (var circle in circles)
        {
            await ProcessPeriodAsync(circle.Id, circle.Name, "month", monthLabel, prevMonth.Year, prevMonth.Month, ct);
            await ProcessPeriodAsync(circle.Id, circle.Name, "year", yearLabel, now.Year - 1, null, ct);
        }
    }

    private async Task ProcessPeriodAsync(
        Guid circleId, string circleName,
        string periodType, string periodLabel,
        int year, int? month,
        CancellationToken ct)
    {
        var alreadyDone = await db.AwardNotificationsSent
            .AnyAsync(a => a.CircleId == circleId && a.PeriodType == periodType && a.PeriodLabel == periodLabel, ct);

        if (alreadyDone)
            return;

        bool emailSent = false;
        try
        {
            var result = await calculator.ComputePeriodAsync(circleId, periodType, year, month);

            if (result.Winner != null)
            {
                var winnerEmail = await db.Users
                    .Where(u => u.Id == result.Winner.UserId)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(ct);

                if (winnerEmail != null)
                {
                    var humanLabel = BuildHumanLabel(periodType, periodLabel);
                    await emailService.SendAwardWinnerEmailAsync(
                        winnerEmail, result.Winner.Name, circleName,
                        humanLabel, result.Winner.NetGain, result.Winner.MatchesPlayed);
                    emailSent = true;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invio notifica premio fallito per circolo {CircleId} periodo {PeriodType}/{PeriodLabel}",
                circleId, periodType, periodLabel);
        }

        db.AwardNotificationsSent.Add(new AwardNotificationSent
        {
            CircleId    = circleId,
            PeriodType  = periodType,
            PeriodLabel = periodLabel,
            EmailSent   = emailSent,
        });
        await db.SaveChangesAsync(ct);
    }

    private static string BuildHumanLabel(string periodType, string periodLabel)
    {
        if (periodType == "year")
            return $"Anno {periodLabel}";

        var parts = periodLabel.Split('-');
        var year = parts[0];
        var month = int.Parse(parts[1]);
        string[] months = ["Gennaio", "Febbraio", "Marzo", "Aprile", "Maggio", "Giugno",
                           "Luglio", "Agosto", "Settembre", "Ottobre", "Novembre", "Dicembre"];
        return $"{months[month - 1]} {year}";
    }
}
