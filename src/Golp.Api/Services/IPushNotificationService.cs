namespace Golp.Api.Services;

public interface IPushNotificationService
{
    Task SendConfirmationRequestAsync(Guid matchId, Guid circleId, Guid[] recipientUserIds);

    /// <summary>Invia una push di prova ai token registrati dell'utente. Ritorna false se nessun token trovato.</summary>
    Task<bool> SendTestNotificationAsync(Guid userId);
}
