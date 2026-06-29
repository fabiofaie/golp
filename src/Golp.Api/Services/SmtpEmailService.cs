using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Golp.Api.Services;

public class SmtpEmailService(
    IConfiguration configuration,
    IEmailTemplateRenderer renderer,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    private readonly string _host = configuration["Smtp:Host"] ?? throw new InvalidOperationException("Smtp:Host non configurato");
    private readonly int _port = int.Parse(configuration["Smtp:Port"] ?? "587");
    private readonly string _user = configuration["Smtp:User"] ?? throw new InvalidOperationException("Smtp:User non configurato");
    private readonly string _password = configuration["Smtp:Password"] ?? throw new InvalidOperationException("Smtp:Password non configurato");
    private readonly string _fromAddress = configuration["Smtp:From"] ?? configuration["Smtp:User"]!;
    private readonly string _fromName = configuration["Smtp:FromName"] ?? "GOLP";
    private readonly bool _useSsl = bool.Parse(configuration["Smtp:UseSsl"] ?? "true");

    public Task SendPasswordResetEmailAsync(string email, string resetLink) =>
        SendAsync(email, "Reimposta la password GOLP", "password-reset", new Dictionary<string, string>
        {
            ["ResetLink"] = resetLink,
        });

    public Task SendCircleActivationEmailAsync(string email, string circleName, string activationLink) =>
        SendAsync(email, $"Sei stato invitato al circolo {circleName} su GOLP", "circle-activation", new Dictionary<string, string>
        {
            ["CircleName"] = circleName,
            ["ActivationLink"] = activationLink,
        });

    public Task SendAddedToCircleNotificationAsync(string email, string circleName) =>
        SendAsync(email, $"Sei stato aggiunto al circolo {circleName}", "added-to-circle", new Dictionary<string, string>
        {
            ["CircleName"] = circleName,
        });

    public Task SendMatchConfirmationRequestEmailAsync(string email, string circleName, string matchLink) =>
        SendAsync(email, $"Conferma richiesta per una partita in {circleName}", "match-confirmation-request", new Dictionary<string, string>
        {
            ["CircleName"] = circleName,
            ["MatchLink"] = matchLink,
        });

    public Task SendMatchDisputedEmailAsync(string email, string circleName, string matchLink) =>
        SendAsync(email, $"Partita contestata in {circleName}", "match-disputed", new Dictionary<string, string>
        {
            ["CircleName"] = circleName,
            ["MatchLink"] = matchLink,
        });

    public Task SendAwardWinnerEmailAsync(string email, string winnerName, string circleName, string humanPeriodLabel, int netGain, int matchesPlayed) =>
        SendAsync(email, $"Sei il giocatore del {humanPeriodLabel} in {circleName}!", "award-winner", new Dictionary<string, string>
        {
            ["WinnerName"]       = winnerName,
            ["CircleName"]       = circleName,
            ["HumanPeriodLabel"] = humanPeriodLabel,
            ["NetGain"]          = netGain.ToString(),
            ["MatchesPlayed"]    = matchesPlayed.ToString(),
        });

    private async Task SendAsync(string toEmail, string subject, string templateName, Dictionary<string, string> values)
    {
        var htmlBody = renderer.Render(templateName, values);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromAddress));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(_host, _port, _useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None);
            await client.AuthenticateAsync(_user, _password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Invio email a {Email} fallito", toEmail);
            throw;
        }
    }
}
