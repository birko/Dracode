using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Server.Services;

/// <summary>Text sends for the <c>/dragon</c>, <c>/wyvern</c> and <c>/kobold</c> endpoints.</summary>
public static class WebSocketSend
{
    /// <summary>
    /// Sends one whole text message. Returns false, and logs, when the socket is not open or the send fails, so a
    /// caller never takes an undelivered message for a delivered one. Concurrent sends on one socket are serialized
    /// by the socket itself (Birko TASK-537 measured it on .NET 10), so there is no send queue.
    /// </summary>
    public static async Task<bool> TrySendTextAsync(this WebSocket socket, byte[] utf8, ILogger logger, CancellationToken cancellationToken = default)
    {
        if (socket.State != WebSocketState.Open)
        {
            logger.LogDebug("WebSocket message not sent: the socket is {State}", socket.State);
            return false;
        }

        try
        {
            await socket.SendAsync(new ArraySegment<byte>(utf8), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or OperationCanceledException)
        {
            logger.LogDebug(ex, "WebSocket message not sent: the socket is {State}", socket.State);
            return false;
        }
    }
}
