using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Client;

/// <summary>
/// Relays a browser WebSocket on the client host to the same path on the KoboldLair server (TASK-119).
/// The caller's own <c>?token=</c> is forwarded URL-encoded; the host never attaches a token of its own.
/// The server certificate is validated unless <c>KoboldLair:AllowInvalidServerCertificate</c> is set in Development.
/// </summary>
public static class ServerWebSocketProxy
{
    public const string AllowInvalidCertificateKey = "KoboldLair:AllowInvalidServerCertificate";

    public static Uri BuildUpstreamUri(string serverUrl, string path, string? callerToken)
    {
        var url = serverUrl.Replace("https://", "wss://").Replace("http://", "ws://").TrimEnd('/') + path;
        if (!string.IsNullOrEmpty(callerToken))
            url += "?token=" + Uri.EscapeDataString(callerToken);
        return new Uri(url);
    }

    /// <summary>True only when the opt-out is set and the host runs in Development; anywhere else it is ignored.</summary>
    public static bool AllowsInvalidCertificate(IConfiguration configuration, IHostEnvironment environment) =>
        environment.IsDevelopment() && configuration.GetValue<bool>(AllowInvalidCertificateKey);

    public static void Map(WebApplication app, string path, string serverUrl)
    {
        var allowInvalidCertificate = AllowsInvalidCertificate(app.Configuration, app.Environment);
        if (allowInvalidCertificate)
            app.Logger.LogWarning(
                "{Key} is set: the {Path} proxy accepts any server certificate. Development only.",
                AllowInvalidCertificateKey, path);

        app.Map(path, async (HttpContext context) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = 400;
                return;
            }

            using var serverWs = new ClientWebSocket();
            if (allowInvalidCertificate)
                serverWs.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;

            try
            {
                await serverWs.ConnectAsync(
                    BuildUpstreamUri(serverUrl, path, context.Request.Query["token"].ToString()),
                    context.RequestAborted);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "WebSocket proxy could not connect {Path} to the server", path);
                context.Response.StatusCode = 502;
                return;
            }

            using var clientWs = await context.WebSockets.AcceptWebSocketAsync();
            await RelayAsync(clientWs, serverWs, context.RequestAborted);
        });
    }

    /// <summary>Relays both ways until one side closes, then closes the other with the same status and description.</summary>
    public static async Task RelayAsync(WebSocket client, WebSocket server, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var clientToServer = PumpAsync(client, server, cts.Token);
        var serverToClient = PumpAsync(server, client, cts.Token);
        // The other direction still carries the peer's close reply, so give it time to arrive before cancelling.
        await Task.WhenAny(clientToServer, serverToClient);
        cts.CancelAfter(CloseHandshakeTimeout);
        await Task.WhenAll(clientToServer, serverToClient);
    }

    private static readonly TimeSpan CloseHandshakeTimeout = TimeSpan.FromSeconds(5);

    private static async Task PumpAsync(WebSocket source, WebSocket destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[4 * 1024];
        try
        {
            while (source.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                var result = await source.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await CloseAsync(destination,
                        result.CloseStatus ?? WebSocketCloseStatus.NormalClosure, result.CloseStatusDescription);
                    return;
                }

                await destination.SendAsync(
                    new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage,
                    cancellationToken);
            }
        }
        catch (WebSocketException)
        {
            // The source dropped without a close frame; tell the other side the connection ended abnormally.
            await CloseAsync(destination, WebSocketCloseStatus.EndpointUnavailable, "Peer disconnected");
        }
        catch (OperationCanceledException)
        {
            // Request aborted, or the peer never answered a close in time.
        }
    }

    private static async Task CloseAsync(WebSocket socket, WebSocketCloseStatus status, string? description)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
            return; // already closing: this close frame is the peer's reply to one we forwarded
        try
        {
            await socket.CloseOutputAsync(status, description, CancellationToken.None);
        }
        catch (WebSocketException)
        {
        }
    }
}
