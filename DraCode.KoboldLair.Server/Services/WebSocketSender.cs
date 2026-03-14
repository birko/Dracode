using System.Collections.Concurrent;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Server.Services;

/// <summary>
/// Queue-based WebSocket sender with concurrency protection and error surfacing.
/// Inspired by Birko.Communication.SSE.SseClientConnection pattern:
/// messages are enqueued and sent by a background loop, preventing concurrent writes
/// and providing reliable error detection.
/// </summary>
public class WebSocketSender : IDisposable
{
    private readonly WebSocket _webSocket;
    private readonly ConcurrentQueue<SendItem> _sendQueue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _sendTask;
    private readonly TimeSpan _sendTimeout = TimeSpan.FromSeconds(30);
    private readonly ILogger? _logger;
    private long _sequenceNumber;
    private bool _disposed;

    /// <summary>
    /// Raised when a send fails (connection lost, timeout, etc.)
    /// </summary>
    public event Action<Exception>? OnSendFailed;

    /// <summary>
    /// Raised when the connection is detected as broken during send.
    /// </summary>
    public event Action? OnDisconnected;

    /// <summary>
    /// Whether the sender is still connected and able to send.
    /// </summary>
    public bool IsConnected => !_disposed && _webSocket.State == WebSocketState.Open;

    public WebSocketSender(WebSocket webSocket, ILogger? logger = null)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _logger = logger;
        _sendTask = Task.Run(SendLoopAsync);
    }

    /// <summary>
    /// Get next sequence number (atomic increment) for ordering detection.
    /// </summary>
    public long GetNextSequence()
    {
        return Interlocked.Increment(ref _sequenceNumber);
    }

    /// <summary>
    /// Enqueue data to be sent over the WebSocket. Non-blocking.
    /// Returns false if the sender is disposed or WebSocket is not open.
    /// </summary>
    public bool Send(byte[] data)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open) return false;

        _sendQueue.Enqueue(new SendItem(data));
        _signal.Release();
        return true;
    }

    /// <summary>
    /// Enqueue data and wait for it to be sent (or fail).
    /// </summary>
    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open) return;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));

        _sendQueue.Enqueue(new SendItem(data, tcs));
        _signal.Release();

        await tcs.Task;
    }

    /// <summary>
    /// Get the number of messages waiting to be sent.
    /// </summary>
    public int QueueDepth => _sendQueue.Count;

    private async Task SendLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                // Wait for a signal that something is in the queue
                await _signal.WaitAsync(_cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_sendQueue.TryDequeue(out var item))
                continue;

            if (_disposed || _webSocket.State != WebSocketState.Open)
            {
                item.Completion?.TrySetResult();
                continue;
            }

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
                timeoutCts.CancelAfter(_sendTimeout);

                await _webSocket.SendAsync(
                    new ArraySegment<byte>(item.Data),
                    WebSocketMessageType.Text,
                    true,
                    timeoutCts.Token);

                item.Completion?.TrySetResult();
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                // Normal shutdown - complete any waiters
                item.Completion?.TrySetCanceled();
                break;
            }
            catch (OperationCanceledException ex)
            {
                // Send timeout
                _logger?.LogWarning("WebSocket send timed out after {Timeout}s", _sendTimeout.TotalSeconds);
                item.Completion?.TrySetException(ex);
                OnSendFailed?.Invoke(ex);
            }
            catch (WebSocketException ex)
            {
                // Connection broken - surface the error
                _logger?.LogWarning(ex, "WebSocket send failed: {State}", _webSocket.State);
                item.Completion?.TrySetResult(); // Don't throw to caller on connection loss
                OnSendFailed?.Invoke(ex);
                OnDisconnected?.Invoke();

                // Drain remaining queue items without sending
                DrainQueue();
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error in WebSocket send loop");
                item.Completion?.TrySetException(ex);
                OnSendFailed?.Invoke(ex);
            }
        }
    }

    private void DrainQueue()
    {
        while (_sendQueue.TryDequeue(out var remaining))
        {
            remaining.Completion?.TrySetResult();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        DrainQueue();

        try { _sendTask.Wait(TimeSpan.FromSeconds(5)); } catch { /* shutdown */ }

        _cts.Dispose();
        _signal.Dispose();
    }

    private record SendItem(byte[] Data, TaskCompletionSource? Completion = null);
}
