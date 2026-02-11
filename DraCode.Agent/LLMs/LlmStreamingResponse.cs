namespace DraCode.Agent
{
    /// <summary>
    /// Response from LLM provider for streaming requests.
    /// Supports both text streaming and tool call capture.
    /// </summary>
    public class LlmStreamingResponse
    {
        /// <summary>
        /// Async enumerable stream of response chunks (text only).
        /// Call this to consume the stream, then check FinalResponse for tool calls.
        /// </summary>
        public required Func<Task<IAsyncEnumerable<string>>> GetStreamAsync { get; set; }

        /// <summary>
        /// Stop reason if known upfront (usually null for streaming, populated after stream completes)
        /// </summary>
        public string? StopReason { get; set; }

        /// <summary>
        /// Error message if streaming failed
        /// </summary>
        public string? Error { get; set; }

        /// <summary>
        /// Whether the stream completed successfully
        /// </summary>
        public bool IsComplete { get; set; }

        /// <summary>
        /// Full response after streaming completes, including any tool calls.
        /// This is populated by the provider during streaming and available after GetStreamAsync completes.
        /// Check this for tool_use content blocks to continue the agent loop.
        /// </summary>
        public LlmResponse? FinalResponse { get; set; }

        /// <summary>
        /// Accumulated text content from streaming (for convenience).
        /// Populated during streaming by providers that support it.
        /// </summary>
        public string? AccumulatedText { get; set; }
    }
}
