using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Models.Validation
{
    /// <summary>
    /// Phase 3: Interface for step completion validators.
    /// Validators check if a step's work has been properly completed.
    /// </summary>
    public interface IStepValidator
    {
        /// <summary>
        /// Gets the name of this validator
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Validates whether a step has been completed successfully
        /// </summary>
        /// <param name="step">The step to validate</param>
        /// <param name="workingDirectory">The working directory for file operations</param>
        /// <returns>Validation result with success status and any issues found</returns>
        Task<ValidationResult> ValidateAsync(ImplementationStep step, string workingDirectory);
    }

    /// <summary>
    /// Result of step validation
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Whether validation passed
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// List of issues found (empty if Success = true)
        /// </summary>
        public List<string> Issues { get; set; } = new();

        /// <summary>
        /// Name of validator that produced this result
        /// </summary>
        public string ValidatorName { get; set; } = string.Empty;
    }
}
