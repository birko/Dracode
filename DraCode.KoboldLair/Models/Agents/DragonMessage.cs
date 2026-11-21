namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Message format from Dragon frontend
    /// </summary>
    public class DragonMessage
    {
        public string? Type { get; set; }
        /// <summary>
        /// Alternative to Type - client may send { action: 'ping' } instead of { type: 'ping' }
        /// </summary>
        public string? Action { get; set; }
        public string Message { get; set; } = "";
        public string? SessionId { get; set; }
        public string? Provider { get; set; }
        /// <summary>For <c>answer_decision</c>: the decision answered (TASK-024).</summary>
        public string? DecisionId { get; set; }
        /// <summary>For <c>answer_decision</c>: the person's answer, an option's text or free text.</summary>
        public string? Answer { get; set; }
        /// <summary>For <c>select_project</c>: the project the client opened (TASK-151).</summary>
        public string? ProjectId { get; set; }
    }
}
