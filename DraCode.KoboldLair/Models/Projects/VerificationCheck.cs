namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Represents a single verification check executed during project verification
    /// </summary>
    public class VerificationCheck
    {
        /// <summary>
        /// Type of check (e.g., "build", "test", "lint")
        /// </summary>
        public string CheckType { get; set; } = "";

        /// <summary>
        /// Command that was executed
        /// </summary>
        public string Command { get; set; } = "";

        /// <summary>
        /// Exit code from command execution
        /// </summary>
        public int ExitCode { get; set; }

        /// <summary>
        /// Output from command execution (stdout + stderr)
        /// </summary>
        public string Output { get; set; } = "";

        /// <summary>
        /// Duration of check execution in seconds
        /// </summary>
        public double DurationSeconds { get; set; }

        /// <summary>
        /// Whether the check passed
        /// </summary>
        public bool Passed { get; set; }

        /// <summary>
        /// Priority level of this check
        /// </summary>
        public VerificationCheckPriority Priority { get; set; } = VerificationCheckPriority.Medium;

        /// <summary>
        /// When the check was executed
        /// </summary>
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Priority levels for verification checks
    /// </summary>
    public enum VerificationCheckPriority
    {
        /// <summary>
        /// Build/compilation checks (must pass)
        /// </summary>
        Critical,

        /// <summary>
        /// Unit tests, integration tests
        /// </summary>
        High,

        /// <summary>
        /// Linting, code style
        /// </summary>
        Medium,

        /// <summary>
        /// Documentation checks, coverage reports
        /// </summary>
        Low
    }
}
