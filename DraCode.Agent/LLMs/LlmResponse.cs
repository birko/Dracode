namespace DraCode.Agent
{
    public class LlmResponse
    {
        public string? StopReason { get; set; }
        public List<ContentBlock>? Content { get; set; }

        /// <summary>
        /// Detailed error message from the LLM provider when StopReason is "error".
        /// Includes the actual API error (e.g., "Insufficient balance", "Rate limit exceeded").
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Creates an error LlmResponse with the given error message.
        /// </summary>
        public static LlmResponse Error(string? errorMessage = null) => new()
        {
            StopReason = "error",
            Content = [],
            ErrorMessage = errorMessage
        };
    }
}
