using DraCode.KoboldLair.Models.Agents;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Models.Validation
{
    /// <summary>
    /// Phase 3: Service for managing and executing step validators
    /// </summary>
    public class StepValidationService
    {
        private readonly List<IStepValidator> _validators;
        private readonly ILogger<StepValidationService>? _logger;

        /// <summary>
        /// Creates a new validation service with default validators
        /// </summary>
        public StepValidationService(ILogger<StepValidationService>? logger = null)
        {
            _validators = new List<IStepValidator>
            {
                new FileCreationValidator(),
                new FileModificationValidator(),
                new ContentExpectationValidator()
            };
            _logger = logger;
        }

        /// <summary>
        /// Adds a custom validator
        /// </summary>
        public void AddValidator(IStepValidator validator)
        {
            _validators.Add(validator);
        }

        /// <summary>
        /// Removes a validator by name
        /// </summary>
        public bool RemoveValidator(string validatorName)
        {
            return _validators.RemoveAll(v => v.Name == validatorName) > 0;
        }

        /// <summary>
        /// Validates a step using all registered validators
        /// </summary>
        /// <param name="step">The step to validate</param>
        /// <param name="workingDirectory">Working directory for file operations</param>
        /// <returns>Combined validation result</returns>
        public async Task<CombinedValidationResult> ValidateStepAsync(ImplementationStep step, string workingDirectory)
        {
            var results = new List<ValidationResult>();
            var allIssues = new List<string>();

            foreach (var validator in _validators)
            {
                try
                {
                    step.Metrics.ValidationAttempts++;
                    var result = await validator.ValidateAsync(step, workingDirectory);
                    results.Add(result);

                    if (!result.Success)
                    {
                        allIssues.AddRange(result.Issues.Select(i => $"[{validator.Name}] {i}"));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Validator {ValidatorName} failed for step {StepIndex}", 
                        validator.Name, step.Index);
                    
                    allIssues.Add($"[{validator.Name}] Validation error: {ex.Message}");
                }
            }

            var success = allIssues.Count == 0;
            return new CombinedValidationResult
            {
                Success = success,
                AllIssues = allIssues,
                IndividualResults = results
            };
        }
    }

    /// <summary>
    /// Combined result from multiple validators
    /// </summary>
    public class CombinedValidationResult
    {
        public bool Success { get; set; }
        public List<string> AllIssues { get; set; } = new();
        public List<ValidationResult> IndividualResults { get; set; } = new();
    }
}
