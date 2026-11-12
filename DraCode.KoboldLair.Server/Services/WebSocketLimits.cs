using Birko.Communication.WebSocket.Messaging;

namespace DraCode.KoboldLair.Server.Services;

/// <summary>Limits shared by the <c>/dragon</c>, <c>/wyvern</c> and <c>/kobold</c> endpoints.</summary>
public static class WebSocketLimits
{
    /// <summary>
    /// The largest message a client may send: Birko's default, 4 MiB. A larger one closes the socket with
    /// MessageTooBig (TASK-135).
    /// </summary>
    public const int MaxMessageBytes = WebSocketMessageExtensions.DefaultMaxMessageBytes;
}
