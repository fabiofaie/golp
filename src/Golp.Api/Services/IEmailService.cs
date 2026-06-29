namespace Golp.Api.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string email, string resetLink);
    Task SendCircleActivationEmailAsync(string email, string circleName, string activationLink);
    Task SendAddedToCircleNotificationAsync(string email, string circleName);
    Task SendMatchConfirmationRequestEmailAsync(string email, string circleName, string matchLink);
    Task SendMatchDisputedEmailAsync(string email, string circleName, string matchLink);
    Task SendAwardWinnerEmailAsync(string email, string winnerName, string circleName, string humanPeriodLabel, int netGain, int matchesPlayed);
}
