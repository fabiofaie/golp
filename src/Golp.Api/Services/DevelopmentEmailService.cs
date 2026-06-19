namespace Golp.Api.Services;

public class DevelopmentEmailService(
    IEmailTemplateRenderer renderer,
    ILogger<DevelopmentEmailService> logger) : IEmailService
{
    public Task SendPasswordResetEmailAsync(string email, string resetLink) =>
        LogAsync(email, "password-reset", new Dictionary<string, string> { ["ResetLink"] = resetLink });

    public Task SendCircleActivationEmailAsync(string email, string circleName, string activationLink) =>
        LogAsync(email, "circle-activation", new Dictionary<string, string>
        {
            ["CircleName"] = circleName,
            ["ActivationLink"] = activationLink,
        });

    public Task SendAddedToCircleNotificationAsync(string email, string circleName) =>
        LogAsync(email, "added-to-circle", new Dictionary<string, string> { ["CircleName"] = circleName });

    public Task SendMatchConfirmationRequestEmailAsync(string email, string circleName, string matchLink) =>
        LogAsync(email, "match-confirmation-request", new Dictionary<string, string>
        {
            ["CircleName"] = circleName,
            ["MatchLink"] = matchLink,
        });

    public Task SendMatchDisputedEmailAsync(string email, string circleName, string matchLink) =>
        LogAsync(email, "match-disputed", new Dictionary<string, string>
        {
            ["CircleName"] = circleName,
            ["MatchLink"] = matchLink,
        });

    private Task LogAsync(string email, string templateName, Dictionary<string, string> values)
    {
        var html = renderer.Render(templateName, values);
        logger.LogInformation("EMAIL [{Template}] to {Email}:\n{Html}", templateName, email, html);
        return Task.CompletedTask;
    }
}
