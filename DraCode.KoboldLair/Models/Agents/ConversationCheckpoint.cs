using System.Text.Json;

namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Persisted conversation state for a Kobold's LLM interaction.
    /// Saved after each step completion and on graceful shutdown to enable
    /// resumption without losing reasoning context.
    /// </summary>
    public class ConversationCheckpoint
    {
        /// <summary>
        /// Task identifier this checkpoint belongs to
        /// </summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>
        /// Project identifier
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// Plan step index at time of save
        /// </summary>
        public int StepIndex { get; set; }

        /// <summary>
        /// When the checkpoint was saved
        /// </summary>
        public DateTime SavedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Serialized conversation messages
        /// </summary>
        public List<CheckpointMessage> Messages { get; set; } = new();
    }

    /// <summary>
    /// A single message in a conversation checkpoint.
    /// Content is stored as JsonElement to safely round-trip any content type
    /// (string, array of content blocks, tool results, etc.)
    /// </summary>
    public class CheckpointMessage
    {
        /// <summary>
        /// Message role (user, assistant, etc.)
        /// </summary>
        public string? Role { get; set; }

        /// <summary>
        /// Message content, serialized as JsonElement for safe storage
        /// </summary>
        public JsonElement? Content { get; set; }
    }
}
