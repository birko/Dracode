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
    }
}
