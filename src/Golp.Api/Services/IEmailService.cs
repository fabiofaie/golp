namespace Golp.Api.Services;

public interface IEmailService
{
    Task SendPasswordResetEmailAsync(string email, string resetLink);
}
