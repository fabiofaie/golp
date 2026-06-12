using FirebaseAdmin.Messaging;

namespace Golp.Api.Services;

public class FirebaseFcmSender : IFcmSender
{
    public async Task<IReadOnlyList<FcmSendResult>> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data)
    {
        var message = new MulticastMessage
        {
            Tokens = tokens.ToList(),
            Notification = new Notification { Title = title, Body = body },
            Data = data.ToDictionary(kv => kv.Key, kv => kv.Value)
        };

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);

        return response.Responses
            .Select((r, i) => new FcmSendResult(
                tokens[i],
                r.IsSuccess,
                r.Exception?.MessagingErrorCode == MessagingErrorCode.Unregistered))
            .ToList();
    }
}
