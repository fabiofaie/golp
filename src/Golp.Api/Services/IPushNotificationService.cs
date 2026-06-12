namespace Golp.Api.Services;

public interface IPushNotificationService
{
    Task SendConfirmationRequestAsync(Guid matchId, Guid circleId, Guid[] recipientUserIds);
}
