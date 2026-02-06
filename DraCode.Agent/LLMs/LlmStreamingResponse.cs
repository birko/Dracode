namespace DraCode.Agent
{
    /// <summary>
    /// Response from LLM provider for streaming requests
    /// </summary>
    public class LlmStreamingResponse
    {
        /// <summary>
        /// Async enumerable stream of response chunks
        /// </summary>
        public required Func<Task<IAsyncEnumerable<string>>> GetStreamAsync { get; set; }

        /// <summary>
        /// Stop reason if known upfront (usually null for streaming)
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
    }
}
