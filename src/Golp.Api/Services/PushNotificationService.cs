using Golp.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Golp.Api.Services;

public class PushNotificationService(
    AppDbContext db,
    IFcmSender fcmSender,
    ILogger<PushNotificationService> logger) : IPushNotificationService
{
    public async Task SendConfirmationRequestAsync(Guid matchId, Guid circleId, Guid[] recipientUserIds)
    {
        try
        {
            var tokens = await db.FcmTokens
                .Where(t => recipientUserIds.Contains(t.UserId))
                .Select(t => t.Token)
                .Distinct()
                .ToListAsync();

            if (tokens.Count == 0)
                return;

            var data = new Dictionary<string, string>
            {
                ["matchId"] = matchId.ToString(),
                ["circleId"] = circleId.ToString()
            };

            var results = await fcmSender.SendAsync(
                tokens,
                "Partita da confermare",
                "Una nuova partita ti aspetta: conferma il risultato!",
                data);

            var deadTokens = results
                .Where(r => r.IsUnregistered)
                .Select(r => r.Token)
                .ToList();

            if (deadTokens.Count > 0)
            {
                var toRemove = await db.FcmTokens
                    .Where(t => deadTokens.Contains(t.Token))
                    .ToListAsync();
                db.FcmTokens.RemoveRange(toRemove);
                await db.SaveChangesAsync();
                logger.LogInformation("Removed {Count} unregistered FCM tokens", deadTokens.Count);
            }
        }
        catch (Exception ex)
        {
            // Fire-and-forget: la partita è già persistita, una push fallita non deve propagare errori
            logger.LogWarning(ex, "Push notification failed for match {MatchId}", matchId);
        }
    }

    public async Task<bool> SendTestNotificationAsync(Guid userId)
    {
        var tokens = await db.FcmTokens
            .Where(t => t.UserId == userId)
            .Select(t => t.Token)
            .Distinct()
            .ToListAsync();

        if (tokens.Count == 0)
            return false;

        try
        {
            var results = await fcmSender.SendAsync(
                tokens,
                "Notifica di prova",
                "Le notifiche push funzionano correttamente su questo dispositivo.",
                new Dictionary<string, string>());

            var deadTokens = results
                .Where(r => r.IsUnregistered)
                .Select(r => r.Token)
                .ToList();

            if (deadTokens.Count > 0)
            {
                var toRemove = await db.FcmTokens
                    .Where(t => deadTokens.Contains(t.Token))
                    .ToListAsync();
                db.FcmTokens.RemoveRange(toRemove);
                await db.SaveChangesAsync();
                logger.LogInformation("Removed {Count} unregistered FCM tokens", deadTokens.Count);
            }

            return true;
        }
        catch (Exception ex)
        {
            // Senza credenziali Firebase configurate l'invio fallisce: coerente con SendConfirmationRequestAsync
            logger.LogWarning(ex, "Test push notification failed for user {UserId}", userId);
            return false;
        }
    }
}
