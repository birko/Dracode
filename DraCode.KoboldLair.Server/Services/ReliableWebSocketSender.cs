using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services;

/// <summary>
/// Provides reliable message delivery over WebSocket with acknowledgments and retries.
/// Prevents message loss by queuing messages and tracking delivery status.
/// </summary>
public class ReliableWebSocketSender : IDisposable
{
    private readonly WebSocket _webSocket;
    private readonly ILogger? _logger;
    private readonly ConcurrentQueue<QueuedMessage> _sendQueue;
    private readonly ConcurrentDictionary<string, PendingMessage> _pendingMessages;
    private readonly SemaphoreSlim _sendSemaphore;
    private readonly Timer _retryTimer;
    private readonly CancellationTokenSource _disposeCts;
    private readonly Task _sendLoop;
    
    private long _sequenceNumber;
    private bool _disposed;

    private static readonly JsonSerializerOptions s_writeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private record QueuedMessage(string MessageId, object Data, int Priority, DateTime QueuedAt);
    private record PendingMessage(QueuedMessage Message, int RetryCount, DateTime LastSentAt);

    public ReliableWebSocketSender(WebSocket webSocket, ILogger? logger = null)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _logger = logger;
        _sendQueue = new ConcurrentQueue<QueuedMessage>();
        _pendingMessages = new ConcurrentDictionary<string, PendingMessage>();
        _sendSemaphore = new SemaphoreSlim(1, 1);
        _disposeCts = new CancellationTokenSource();
        
        // Retry timer: check for unacknowledged messages every 2 seconds
        _retryTimer = new Timer(RetryUnacknowledgedMessages, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        
        // Start background send loop
        _sendLoop = Task.Run(SendLoopAsync);
    }

    /// <summary>
    /// Queue a message for reliable delivery with optional priority.
    /// Priority: 0 = highest (critical), 10 = normal (default), 20 = low
    /// </summary>
    public void QueueMessage(string messageId, object data, int priority = 10)
    {
        if (_disposed) return;
        
        var message = new QueuedMessage(messageId, data, priority, DateTime.UtcNow);
        _sendQueue.Enqueue(message);
    }

    /// <summary>
    /// Send a message immediately without queuing (bypasses priority queue).
    /// Still tracks for acknowledgment and retry.
    /// </summary>
    public async Task SendImmediateAsync(string messageId, object data, CancellationToken cancellationToken = default)
    {
        if (_disposed) return;
        
        var message = new QueuedMessage(messageId, data, 0, DateTime.UtcNow);
        await SendMessageInternalAsync(message, cancellationToken);
    }

    /// <summary>
    /// Send raw bytes safely using the internal semaphore to prevent concurrent WebSocket writes.
    /// Does NOT track for acknowledgment/retry - use for messages managed externally.
    /// </summary>
    public async Task SendSafeAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open) return;

        await _sendSemaphore.WaitAsync(cancellationToken);
        try
        {
            await _webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Text, true, cancellationToken);
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    /// <summary>
    /// Acknowledge receipt of a message (called when client sends ack).
    /// </summary>
    public void AcknowledgeMessage(string messageId)
    {
        if (_pendingMessages.TryRemove(messageId, out var pending))
        {
            _logger?.LogDebug("[ReliableWS] Message acknowledged: {MessageId}", messageId);
        }
    }

    /// <summary>
    /// Get current sequence number and increment atomically.
    /// </summary>
    public long GetNextSequence()
    {
        return Interlocked.Increment(ref _sequenceNumber);
    }

    private async Task SendLoopAsync()
    {
        var cancellationToken = _disposeCts.Token;
        
        while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                // Try to dequeue a message (priority-based)
                if (_sendQueue.TryDequeue(out var message))
                {
                    await SendMessageInternalAsync(message, cancellationToken);
                }
                else
                {
                    // No messages, wait a bit
                    await Task.Delay(10, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[ReliableWS] Error in send loop");
                await Task.Delay(100, cancellationToken);
            }
        }
    }

    private async Task SendMessageInternalAsync(QueuedMessage message, CancellationToken cancellationToken)
    {
        if (_webSocket.State != WebSocketState.Open)
        {
            _logger?.LogWarning("[ReliableWS] WebSocket not open, dropping message: {MessageId}", message.MessageId);
            return;
        }

        await _sendSemaphore.WaitAsync(cancellationToken);
        try
        {
            // Add sequence number to message
            var jsonNode = JsonSerializer.SerializeToNode(message.Data, s_writeOptions);
            if (jsonNode is System.Text.Json.Nodes.JsonObject jsonObject)
            {
                jsonObject["messageId"] = message.MessageId;
                jsonObject["sequence"] = GetNextSequence();
            }

            var json = jsonNode?.ToJsonString(s_writeOptions) ?? JsonSerializer.Serialize(message.Data, s_writeOptions);
            var bytes = Encoding.UTF8.GetBytes(json);
            
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
            
            // Track as pending (will be removed when client acknowledges)
            var pending = new PendingMessage(message, 0, DateTime.UtcNow);
            _pendingMessages[message.MessageId] = pending;
            
            _logger?.LogDebug("[ReliableWS] Sent message: {MessageId}, queued for {Elapsed}ms", 
                message.MessageId, (DateTime.UtcNow - message.QueuedAt).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[ReliableWS] Failed to send message: {MessageId}", message.MessageId);
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }

    private void RetryUnacknowledgedMessages(object? state)
    {
        if (_disposed || _webSocket.State != WebSocketState.Open) return;

        var now = DateTime.UtcNow;
        var toRetry = new List<(string MessageId, QueuedMessage Message)>();

        foreach (var kvp in _pendingMessages)
        {
            var messageId = kvp.Key;
            var pending = kvp.Value;
            
            // Retry if message was sent more than 3 seconds ago and retry count < 3
            if ((now - pending.LastSentAt).TotalSeconds > 3 && pending.RetryCount < 3)
            {
                toRetry.Add((messageId, pending.Message));
            }
            // Drop if retry count exceeded
            else if (pending.RetryCount >= 3)
            {
                _logger?.LogWarning("[ReliableWS] Dropping message after 3 retries: {MessageId}", messageId);
                _pendingMessages.TryRemove(messageId, out _);
            }
        }

        // Re-queue messages for retry
        foreach (var (messageId, message) in toRetry)
        {
            _logger?.LogWarning("[ReliableWS] Retrying unacknowledged message: {MessageId}", messageId);
            
            if (_pendingMessages.TryGetValue(messageId, out var existing))
            {
                var updated = new PendingMessage(message, existing.RetryCount + 1, DateTime.UtcNow);
                _pendingMessages[messageId] = updated;
            }
            
            _sendQueue.Enqueue(message);
        }
    }

    /// <summary>
    /// Wait for all queued messages to be sent (does not wait for acknowledgments).
    /// </summary>
    public async Task FlushAsync(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        
        while (!_sendQueue.IsEmpty && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        
        _disposeCts.Cancel();
        _retryTimer.Dispose();
        _sendSemaphore.Dispose();
        _disposeCts.Dispose();
        
        try
        {
            _sendLoop.Wait(TimeSpan.FromSeconds(2));
        }
        catch { }
    }
}
