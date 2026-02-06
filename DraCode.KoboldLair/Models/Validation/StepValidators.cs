using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Models.Validation
{
    /// <summary>
    /// Phase 3: Validates that files expected to be created actually exist
    /// </summary>
    public class FileCreationValidator : IStepValidator
    {
        public string Name => "FileCreation";

        public Task<ValidationResult> ValidateAsync(ImplementationStep step, string workingDirectory)
        {
            var result = new ValidationResult { ValidatorName = Name };
            var issues = new List<string>();

            foreach (var filePath in step.FilesToCreate)
            {
                var fullPath = Path.IsPathRooted(filePath)
                    ? filePath
                    : Path.Combine(workingDirectory, filePath);

                if (!File.Exists(fullPath))
                {
                    issues.Add($"Expected file not created: {filePath}");
                }
            }

            result.Success = issues.Count == 0;
            result.Issues = issues;

            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Phase 3: Validates that files expected to be modified were actually modified
    /// </summary>
    public class FileModificationValidator : IStepValidator
    {
        public string Name => "FileModification";

        public Task<ValidationResult> ValidateAsync(ImplementationStep step, string workingDirectory)
        {
            var result = new ValidationResult { ValidatorName = Name };
            var issues = new List<string>();

            foreach (var filePath in step.FilesToModify)
            {
                var fullPath = Path.IsPathRooted(filePath)
                    ? filePath
                    : Path.Combine(workingDirectory, filePath);

                if (!File.Exists(fullPath))
                {
                    issues.Add($"Expected file not found: {filePath}");
                }
                else if (step.StartedAt.HasValue)
                {
                    // Check if file was modified after step started
                    var lastWrite = File.GetLastWriteTimeUtc(fullPath);
                    if (lastWrite < step.StartedAt.Value)
                    {
                        issues.Add($"File not modified since step started: {filePath}");
                    }
                }
            }

            result.Success = issues.Count == 0;
            result.Issues = issues;

            return Task.FromResult(result);
        }
    }
}
