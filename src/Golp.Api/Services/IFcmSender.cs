namespace Golp.Api.Services;

public record FcmSendResult(string Token, bool IsSuccess, bool IsUnregistered);

public interface IFcmSender
{
    Task<IReadOnlyList<FcmSendResult>> SendAsync(
        IReadOnlyList<string> tokens,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data);
}
