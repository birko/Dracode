using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using DraCode.KoboldLair.Server.Models.Dragon;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Non-blocking request queue for Dragon agent processing.
    /// Processes Dragon requests asynchronously on worker threads, preventing WebSocket blocking.
    /// </summary>
    public class DragonRequestQueue : IDisposable
    {
        private static readonly JsonSerializerOptions s_writeOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly ILogger _logger;
        private readonly Channel<DragonRequest> _requestChannel;
        private readonly ConcurrentDictionary<string, DragonRequest> _activeRequests;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;
        private readonly SemaphoreSlim _concurrencyLimiter;
        private readonly Task _processingTask;
        private readonly CancellationTokenSource _shutdownCts;
        private readonly int _maxConcurrentRequests;
        private readonly TimeSpan _requestTimeout;
        private bool _disposed;

        // Statistics
        private long _totalRequestsProcessed;
        private long _totalRequestsCancelled;
        private long _totalRequestsErrored;
        private DateTime _lastStatisticsReport = DateTime.UtcNow;

        public DragonRequestQueue(
            ILogger logger,
            int maxConcurrentRequests = 5,
            int requestTimeoutSeconds = 300)
        {
            _logger = logger;
            _maxConcurrentRequests = maxConcurrentRequests;
            _requestTimeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
            _concurrencyLimiter = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
            _shutdownCts = new CancellationTokenSource();

            // Create an unbounded channel for requests
            _requestChannel = Channel.CreateUnbounded<DragonRequest>(new UnboundedChannelOptions
            {
                SingleReader = false, // Allow multiple readers for parallel processing
                SingleWriter = true   // Single writer (the enqueue method)
            });

            _activeRequests = new ConcurrentDictionary<string, DragonRequest>();
            _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();

            // Start processing tasks
            _processingTask = StartProcessingAsync(_shutdownCts.Token);

            _logger.LogInformation("DragonRequestQueue initialized: MaxConcurrent={Max}, Timeout={Timeout}s",
                maxConcurrentRequests, requestTimeoutSeconds);
        }

        /// <summary>
        /// Enqueues a Dragon request for asynchronous processing
        /// </summary>
        public async Task<string> EnqueueAsync(DragonRequest request)
        {
            // Cancel any existing request for this session
            await CancelSessionRequestAsync(request.SessionId);

            // Store the cancellation token
            _cancellationTokens[request.SessionId] = request.CancellationTokenSource;

            // Send to channel (non-blocking)
            await _requestChannel.Writer.WriteAsync(request);

            _logger.LogDebug("Request enqueued: {RequestId} for session {SessionId}",
                request.RequestId, request.SessionId);

            return request.RequestId;
        }

        /// <summary>
        /// Cancels the active request for a session
        /// </summary>
        public async Task<bool> CancelSessionRequestAsync(string sessionId)
        {
            if (_cancellationTokens.TryRemove(sessionId, out var cts))
            {
                await Task.Run(() => cts.Cancel());
                cts.Dispose();
                Interlocked.Increment(ref _totalRequestsCancelled);
                _logger.LogDebug("Cancelled request for session: {SessionId}", sessionId);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the current number of queued requests
        /// </summary>
        public int QueuedCount => _activeRequests.Count;

        /// <summary>
        /// Gets statistics about the request queue
        /// </summary>
        public QueueStatistics GetStatistics()
        {
            return new QueueStatistics
            {
                ActiveRequests = _activeRequests.Count,
                TotalProcessed = Interlocked.Read(ref _totalRequestsProcessed),
                TotalCancelled = Interlocked.Read(ref _totalRequestsCancelled),
                TotalErrored = Interlocked.Read(ref _totalRequestsErrored),
                MaxConcurrency = _maxConcurrentRequests,
                AvailableSlots = _concurrencyLimiter.CurrentCount
            };
        }

        /// <summary>
        /// Starts the background processing loop
        /// </summary>
        private async Task StartProcessingAsync(CancellationToken shutdownToken)
        {
            _logger.LogInformation("DragonRequestQueue processing started");

            // Start multiple worker tasks for parallel processing
            var workers = new List<Task>();
            for (int i = 0; i < _maxConcurrentRequests; i++)
            {
                workers.Add(ProcessRequestsWorkerAsync(shutdownToken));
            }

            // Wait for all workers to complete
            await Task.WhenAll(workers);

            _logger.LogInformation("DragonRequestQueue processing stopped");
        }

        /// <summary>
        /// Worker task that processes requests from the channel
        /// </summary>
        private async Task ProcessRequestsWorkerAsync(CancellationToken shutdownToken)
        {
            await foreach (var request in _requestChannel.Reader.ReadAllAsync(shutdownToken))
            {
                // Check if request was already cancelled
                if (request.IsCancelled)
                {
                    _logger.LogDebug("Skipping cancelled request: {RequestId}", request.RequestId);
                    continue;
                }

                // Log queue wait time for debugging
                var queueWaitTime = DateTime.UtcNow - request.QueuedAt;
                _logger.LogInformation("[DragonRequest] QUEUE {RequestId} | Wait: {WaitMs}ms before processing",
                    request.RequestId, queueWaitTime.TotalMilliseconds.ToString("F0"));
                if (queueWaitTime.TotalSeconds > 1)
                {
                    _logger.LogWarning("[DragonRequest] QUEUE DELAY {RequestId} | Waited {WaitTime}s in queue",
                        request.RequestId, queueWaitTime.TotalSeconds.ToString("F2"));
                }

                // Wait for concurrency slot
                var slotWaitStart = DateTime.UtcNow;
                await _concurrencyLimiter.WaitAsync(shutdownToken);
                var slotWaitTime = DateTime.UtcNow - slotWaitStart;
                if (slotWaitTime.TotalMilliseconds > 0)
                {
                    _logger.LogDebug("[DragonRequest] CONCURRENCY {RequestId} | Slot wait: {WaitMs}ms",
                        request.RequestId, slotWaitTime.TotalMilliseconds.ToString("F0"));
                }
                if (slotWaitTime.TotalSeconds > 1)
                {
                    _logger.LogWarning("[DragonRequest] CONCURRENCY DELAY {RequestId} | Waited {WaitTime}s for slot",
                        request.RequestId, slotWaitTime.TotalSeconds.ToString("F2"));
                }

                try
                {
                    // Add to active requests
                    _activeRequests[request.RequestId] = request;

                    // Process with timeout
                    var processingTask = ProcessRequestAsync(request);
                    var timeoutTask = Task.Delay(_requestTimeout, shutdownToken);
                    var completedTask = await Task.WhenAny(processingTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        _logger.LogWarning("Request timeout: {RequestId} after {Timeout}s",
                            request.RequestId, _requestTimeout.TotalSeconds);
                        request.CancellationTokenSource.Cancel();
                        await processingTask.ContinueWith(_ => { }); // Ensure cleanup
                    }
                    else
                    {
                        await processingTask;
                    }

                    // Log statistics periodically
                    await LogStatisticsIfNeededAsync();
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("Request processing cancelled: {RequestId}", request.RequestId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing request: {RequestId}", request.RequestId);
                    Interlocked.Increment(ref _totalRequestsErrored);
                }
                finally
                {
                    // Remove from active requests
                    _activeRequests.TryRemove(request.RequestId, out _);
                    _cancellationTokens.TryRemove(request.SessionId, out var cts);
                    cts?.Dispose();
                    _concurrencyLimiter.Release();
                }
            }
        }

        /// <summary>
        /// Extracts agent name from delegation message
        /// </summary>
        private static string? ExtractAgentName(string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            // Look for patterns like "Sage", "Seeker", "Sentinel", "Warden"
            var agents = new[] { "Sage", "Seeker", "Sentinel", "Warden" };
            foreach (var agent in agents)
            {
                if (message.Contains(agent, StringComparison.OrdinalIgnoreCase))
                    return agent;
            }
            return null;
        }

        /// <summary>
        /// Processes a single Dragon request with detailed latency tracking
        /// </summary>
        private async Task ProcessRequestAsync(DragonRequest request)
        {
            var startTime = DateTime.UtcNow;
            string? response = null;
            bool isStreamed = false;
            bool success = false;
            string? errorType = null;
            string? errorMessage = null;

            // Latency tracking
            var latencyTracker = new LatencyTracker();

            try
            {
                _logger.LogInformation("[DragonRequest] START {RequestId} | Session: {SessionId} | Message: {MessagePreview}",
                    request.RequestId, request.SessionId,
                    request.Message.Length > 50 ? request.Message.Substring(0, 50) + "..." : request.Message);

                // Send initial status
                var statusStart = DateTime.UtcNow;
                await SendStatusAsync(request, DragonStatusUpdate.STATUS_THINKING, "Processing your request...");
                latencyTracker.TrackOperation("InitialStatus", DateTime.UtcNow - statusStart);

                // Set status callback for progress updates with latency tracking
                var delegationStart = DateTime.UtcNow;
                string? currentAgent = "Dragon";
                int llmCallCount = 0;

                request.StatusCallback = async (statusType, message) =>
                {
                    // Track delegation latency
                    if (statusType == "delegation_start")
                    {
                        currentAgent = ExtractAgentName(message) ?? "Unknown";
                        delegationStart = DateTime.UtcNow;
                        _logger.LogDebug("[DragonRequest] {RequestId} | Delegating to {Agent} | Reason: {Message}",
                            request.RequestId, currentAgent, message);
                    }
                    else if (statusType == "delegation_complete")
                    {
                        var delegationDuration = DateTime.UtcNow - delegationStart;
                        latencyTracker.TrackDelegation(currentAgent, delegationDuration);
                        _logger.LogInformation("[DragonRequest] {RequestId} | {Agent} completed in {Duration}ms",
                            request.RequestId, currentAgent, delegationDuration.TotalMilliseconds.ToString("F0"));
                    }
                    else if (statusType == "llm_call_start")
                    {
                        llmCallCount++;
                        _logger.LogDebug("[DragonRequest] {RequestId} | LLM call #{Count} starting for {Agent}",
                            request.RequestId, llmCallCount, currentAgent);
                    }
                    else if (statusType == "llm_call_complete")
                    {
                        _logger.LogDebug("[DragonRequest] {RequestId} | LLM call #{Count} completed for {Agent}",
                            request.RequestId, llmCallCount, currentAgent);
                    }

                    await SendStatusAsync(request, statusType, message);
                };

                // Process the Dragon request
                var dragonStart = DateTime.UtcNow;
                response = await request.Dragon.ContinueSessionAsync(request.Message);
                var dragonDuration = DateTime.UtcNow - dragonStart;
                latencyTracker.TrackOperation("DragonProcessing", dragonDuration);

                isStreamed = request.Dragon.ConversationMessageCount > 0; // Approximation
                success = true;

                var totalDuration = DateTime.UtcNow - startTime;

                // Log detailed latency breakdown
                _logger.LogInformation("[DragonRequest] COMPLETE {RequestId} | Total: {Total}ms | Dragon: {Dragon}ms | LLM Calls: {LLMCount} | Delegations: {Delegations}",
                    request.RequestId,
                    totalDuration.TotalMilliseconds.ToString("F0"),
                    dragonDuration.TotalMilliseconds.ToString("F0"),
                    llmCallCount,
                    latencyTracker.GetDelegationSummary());

                // Log warning if request took too long
                if (totalDuration.TotalSeconds > 60)
                {
                    _logger.LogWarning("[DragonRequest] SLOW {RequestId} | Total: {Total}s | Breakdown: {Breakdown}",
                        request.RequestId,
                        totalDuration.TotalSeconds.ToString("F1"),
                        latencyTracker.GetDetailedBreakdown());
                }
            }
            catch (HttpRequestException ex)
            {
                errorType = "llm_connection";
                errorMessage = $"Failed to connect to LLM provider: {ex.Message}";
                _logger.LogError(ex, "LLM connection error for request: {RequestId}", request.RequestId);
            }
            catch (OperationCanceledException)
            {
                errorType = "cancelled";
                errorMessage = "Request was cancelled";
                _logger.LogDebug("Request cancelled: {RequestId}", request.RequestId);
            }
            catch (Exception ex)
            {
                errorType = "general";
                errorMessage = ex.Message;
                _logger.LogError(ex, "Error processing request: {RequestId}", request.RequestId);
            }
            finally
            {
                var duration = DateTime.UtcNow - startTime;

                if (success)
                {
                    await SendResponseAsync(request, response!, isStreamed);
                    await SendStatusAsync(request, DragonStatusUpdate.STATUS_COMPLETE, "Response complete");
                    Interlocked.Increment(ref _totalRequestsProcessed);
                }
                else
                {
                    await SendErrorAsync(request, errorType ?? "unknown", errorMessage ?? "Unknown error");
                    await SendStatusAsync(request, DragonStatusUpdate.STATUS_ERROR, $"Error: {errorMessage}");
                    Interlocked.Increment(ref _totalRequestsErrored);
                }

                request.IsProcessed = true;
            }
        }

        /// <summary>
        /// Sends a status update to the client
        /// </summary>
        private async Task SendStatusAsync(DragonRequest request, string statusType, string message)
        {
            try
            {
                if (request.WebSocket.State != WebSocketState.Open)
                    return;

                var status = new DragonStatusUpdate
                {
                    RequestId = request.RequestId,
                    SessionId = request.SessionId,
                    StatusType = statusType,
                    Message = message
                };

                var json = JsonSerializer.Serialize(new
                {
                    type = "dragon_status",
                    requestId = request.RequestId,
                    sessionId = request.SessionId,
                    statusType,
                    message,
                    timestamp = DateTime.UtcNow
                }, s_writeOptions);
                await request.Sender.SendAsync(Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to send status update for request: {RequestId}", request.RequestId);
            }
        }

        /// <summary>
        /// Sends a response to the client
        /// </summary>
        private async Task SendResponseAsync(DragonRequest request, string response, bool isStreamed)
        {
            try
            {
                if (request.WebSocket.State != WebSocketState.Open)
                    return;

                var json = JsonSerializer.Serialize(new
                {
                    type = "dragon_message",
                    requestId = request.RequestId,
                    sessionId = request.SessionId,
                    message = response,
                    isStreamed,
                    timestamp = DateTime.UtcNow
                }, s_writeOptions);
                await request.Sender.SendAsync(Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send response for request: {RequestId}", request.RequestId);
            }
        }

        /// <summary>
        /// Sends an error to the client
        /// </summary>
        private async Task SendErrorAsync(DragonRequest request, string errorType, string errorMessage)
        {
            try
            {
                if (request.WebSocket.State != WebSocketState.Open)
                    return;

                var json = JsonSerializer.Serialize(new
                {
                    type = "error",
                    requestId = request.RequestId,
                    sessionId = request.SessionId,
                    errorType,
                    message = errorMessage,
                    timestamp = DateTime.UtcNow
                }, s_writeOptions);
                await request.Sender.SendAsync(Encoding.UTF8.GetBytes(json));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send error for request: {RequestId}", request.RequestId);
            }
        }

        /// <summary>
        /// Logs statistics if enough time has passed
        /// </summary>
        private async Task LogStatisticsIfNeededAsync()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastStatisticsReport).TotalMinutes >= 5)
            {
                _lastStatisticsReport = now;
                var stats = GetStatistics();
                _logger.LogInformation("DragonRequestQueue Statistics: " +
                    "Active={Active}, Processed={Processed}, Cancelled={Cancelled}, Errored={Errored}",
                    stats.ActiveRequests, stats.TotalProcessed, stats.TotalCancelled, stats.TotalErrored);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _shutdownCts.Cancel();
                _processingTask.Wait(TimeSpan.FromSeconds(5));
                _shutdownCts.Dispose();
                _concurrencyLimiter.Dispose();

                // Cancel all active requests
                foreach (var cts in _cancellationTokens.Values)
                {
                    cts.Cancel();
                    cts.Dispose();
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Statistics for the request queue
    /// </summary>
    public class QueueStatistics
    {
        public int ActiveRequests { get; init; }
        public long TotalProcessed { get; init; }
        public long TotalCancelled { get; init; }
        public long TotalErrored { get; init; }
        public int MaxConcurrency { get; init; }
        public int AvailableSlots { get; init; }
    }

    /// <summary>
    /// Tracks detailed latency breakdown for Dragon requests
    /// </summary>
    internal class LatencyTracker
    {
        private readonly Dictionary<string, TimeSpan> _operationTimes = new();
        private readonly Dictionary<string, List<TimeSpan>> _delegationTimes = new();
        private readonly List<string> _delegationOrder = new();

        public void TrackOperation(string operationName, TimeSpan duration)
        {
            _operationTimes[operationName] = duration;
        }

        public void TrackDelegation(string agentName, TimeSpan duration)
        {
            if (!_delegationTimes.ContainsKey(agentName))
            {
                _delegationTimes[agentName] = new List<TimeSpan>();
                _delegationOrder.Add(agentName);
            }
            _delegationTimes[agentName].Add(duration);
        }

        public string GetDelegationSummary()
        {
            if (_delegationTimes.Count == 0)
                return "none";

            var parts = new List<string>();
            foreach (var agent in _delegationOrder)
            {
                var times = _delegationTimes[agent];
                var total = TimeSpan.FromMilliseconds(times.Sum(t => t.TotalMilliseconds));
                parts.Add($"{agent}={total.TotalMilliseconds:F0}ms");
            }
            return string.Join(", ", parts);
        }

        public string GetDetailedBreakdown()
        {
            var parts = new List<string>();

            // Add operation times
            foreach (var (op, time) in _operationTimes)
            {
                parts.Add($"{op}={time.TotalMilliseconds:F0}ms");
            }

            // Add delegation times
            foreach (var (agent, times) in _delegationTimes)
            {
                var total = TimeSpan.FromMilliseconds(times.Sum(t => t.TotalMilliseconds));
                var avg = TimeSpan.FromMilliseconds(times.Average(t => t.TotalMilliseconds));
                parts.Add($"{agent} total={total.TotalMilliseconds:F0}ms (avg={avg.TotalMilliseconds:F0}ms, calls={times.Count})");
            }

            return string.Join(", ", parts);
        }
    }
}
