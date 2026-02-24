using System.Net.WebSockets;

namespace DraCode.KoboldLair.Server.Services;

/// <summary>
/// Simple WebSocket sender with concurrency protection.
/// Uses a semaphore to prevent concurrent WebSocket writes (which .NET WebSocket doesn't support).
/// </summary>
public class WebSocketSender : IDisposable
{
    private readonly WebSocket _webSocket;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);
    private long _sequenceNumber;
    private bool _disposed;

    public WebSocketSender(WebSocket webSocket)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
    }

    /// <summary>
    /// Get next sequence number (atomic increment) for ordering detection.
    /// </summary>
    public long GetNextSequence()
    {
        return Interlocked.Increment(ref _sequenceNumber);
    }

    /// <summary>
    /// Send raw bytes over the WebSocket, protected by semaphore to prevent concurrent writes.
    /// </summary>
    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open) return;

        await _sendSemaphore.WaitAsync(cancellationToken);
        try
        {
            if (_webSocket.State != WebSocketState.Open) return;
            await _webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cancellationToken);
        }
        catch (WebSocketException)
        {
            // WebSocket closed between state check and send - expected during disconnect
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sendSemaphore.Dispose();
    }
}
