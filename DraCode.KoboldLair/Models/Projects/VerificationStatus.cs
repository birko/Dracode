namespace DraCode.KoboldLair.Models.Projects
{
    /// <summary>
    /// Status of project verification phase
    /// </summary>
    public enum VerificationStatus
    {
        /// <summary>
        /// Verification has not been started yet
        /// </summary>
        NotStarted,

        /// <summary>
        /// Verification is currently running
        /// </summary>
        InProgress,

        /// <summary>
        /// Verification completed successfully - all checks passed
        /// </summary>
        Passed,

        /// <summary>
        /// Verification completed but some checks failed
        /// </summary>
        Failed,

        /// <summary>
        /// Verification was skipped (e.g., for imported projects or manual override)
        /// </summary>
        Skipped
    }
}
