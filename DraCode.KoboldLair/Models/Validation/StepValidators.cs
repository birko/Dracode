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

    /// <summary>
    /// Validates that expected content identifiers are present in the step's target files.
    /// Prevents auto-advancement when a file was touched but the actual work wasn't done.
    /// </summary>
    public class ContentExpectationValidator : IStepValidator
    {
        public string Name => "ContentExpectation";

        public async Task<ValidationResult> ValidateAsync(ImplementationStep step, string workingDirectory)
        {
            var result = new ValidationResult { ValidatorName = Name };

            // No expected content defined - pass (backwards compatible)
            if (step.ExpectedContent.Count == 0)
            {
                result.Success = true;
                return result;
            }

            var issues = new List<string>();

            // Build combined set of target files
            var targetFiles = new List<string>();
            targetFiles.AddRange(step.FilesToCreate);
            targetFiles.AddRange(step.FilesToModify);

            if (targetFiles.Count == 0)
            {
                // No target files but has expected content - can't validate, pass
                result.Success = true;
                return result;
            }

            // Read each file's content once and cache it
            var fileContents = new Dictionary<string, string>();
            foreach (var filePath in targetFiles)
            {
                var fullPath = Path.IsPathRooted(filePath)
                    ? filePath
                    : Path.Combine(workingDirectory, filePath);

                if (File.Exists(fullPath))
                {
                    try
                    {
                        fileContents[filePath] = await File.ReadAllTextAsync(fullPath);
                    }
                    catch
                    {
                        // Can't read file - skip it
                    }
                }
            }

            // Check each expected string against all target files
            foreach (var expected in step.ExpectedContent)
            {
                if (string.IsNullOrWhiteSpace(expected))
                    continue;

                bool found = fileContents.Values.Any(content => content.Contains(expected, StringComparison.Ordinal));

                if (!found)
                {
                    var fileList = string.Join(", ", targetFiles);
                    issues.Add($"Expected content not found: '{expected}' in [{fileList}]");
                }
            }

            result.Success = issues.Count == 0;
            result.Issues = issues;

            return result;
        }
    }
}
