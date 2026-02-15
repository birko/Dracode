namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Represents a verification step recommended by Wyrm for project validation
    /// </summary>
    public class VerificationStepDefinition
    {
        /// <summary>
        /// Type of verification check (e.g., "build", "test", "lint")
        /// </summary>
        public string CheckType { get; set; } = "";

        /// <summary>
        /// Command to execute
        /// </summary>
        public string Command { get; set; } = "";

        /// <summary>
        /// Success criteria (e.g., "exit_code_0", "contains:All tests passed")
        /// </summary>
        public string SuccessCriteria { get; set; } = "exit_code_0";

        /// <summary>
        /// Priority of this check
        /// </summary>
        public string Priority { get; set; } = "Medium";

        /// <summary>
        /// Timeout in seconds (0 = no timeout)
        /// </summary>
        public int TimeoutSeconds { get; set; } = 300;

        /// <summary>
        /// Working directory for command execution (relative to project workspace)
        /// </summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>
        /// Description of what this check validates
        /// </summary>
        public string Description { get; set; } = "";
    }
}
