namespace Golp.Api.Services;

public class DevelopmentEmailService(ILogger<DevelopmentEmailService> logger) : IEmailService
{
    public Task SendPasswordResetEmailAsync(string email, string resetLink)
    {
        logger.LogInformation("PASSWORD RESET LINK for {Email}: {Link}", email, resetLink);
        return Task.CompletedTask;
    }
}
