using System.Net.WebSockets;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Server.Services;

namespace DraCode.KoboldLair.Server.Models.Dragon
{
    /// <summary>
    /// Represents a pending Dragon request to be processed asynchronously
    /// </summary>
    public class DragonRequest
    {
        public required string RequestId { get; init; }
        public required string SessionId { get; init; }
        public required string Message { get; init; }
        public required System.Net.WebSockets.WebSocket WebSocket { get; init; }
        public required WebSocketSender Sender { get; init; }
        public required CancellationTokenSource CancellationTokenSource { get; init; }
        public required DateTime QueuedAt { get; init; }
        public required DragonAgent Dragon { get; init; }

        /// <summary>
        /// Optional callback for status updates during processing
        /// </summary>
        public Action<string, string>? StatusCallback { get; set; }

        /// <summary>
        /// Optional callback called after the response is successfully sent
        /// Used for post-response processing like checking for new specifications
        /// </summary>
        public Func<Task>? OnResponseCallback { get; set; }

        /// <summary>
        /// Whether this request has been processed
        /// </summary>
        public bool IsProcessed { get; set; }

        /// <summary>
        /// Whether this request was cancelled
        /// </summary>
        public bool IsCancelled => CancellationTokenSource.IsCancellationRequested;
    }

    /// <summary>
    /// Result of a Dragon request processing
    /// </summary>
    public class DragonRequestResult
    {
        public required string RequestId { get; init; }
        public required string SessionId { get; init; }
        public required string Response { get; init; }
        public bool IsStreamed { get; init; }
        public bool Success { get; init; }
        public string? ErrorType { get; init; }
        public string? ErrorMessage { get; init; }
        public DateTime ProcessedAt { get; init; }
        public TimeSpan ProcessingDuration { get; init; }

        public static DragonRequestResult SuccessResult(string requestId, string sessionId, string response, bool isStreamed, TimeSpan duration) =>
            new()
            {
                RequestId = requestId,
                SessionId = sessionId,
                Response = response,
                IsStreamed = isStreamed,
                Success = true,
                ProcessedAt = DateTime.UtcNow,
                ProcessingDuration = duration
            };

        public static DragonRequestResult ErrorResult(string requestId, string sessionId, string errorType, string errorMessage, TimeSpan duration) =>
            new()
            {
                RequestId = requestId,
                SessionId = sessionId,
                Response = "",
                Success = false,
                ErrorType = errorType,
                ErrorMessage = errorMessage,
                ProcessedAt = DateTime.UtcNow,
                ProcessingDuration = duration
            };
    }

    /// <summary>
    /// Status update sent during Dragon request processing
    /// </summary>
    public class DragonStatusUpdate
    {
        public required string RequestId { get; init; }
        public required string SessionId { get; init; }
        public required string StatusType { get; init; }
        public required string Message { get; init; }
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;

        // Status types: "thinking", "delegating", "processing", "complete"
        public const string STATUS_THINKING = "thinking";
        public const string STATUS_DELEGATING = "delegating";
        public const string STATUS_PROCESSING = "processing";
        public const string STATUS_COMPLETE = "complete";
        public const string STATUS_ERROR = "error";
    }
}
