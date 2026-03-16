using Birko.Validation;

namespace DraCode.KoboldLair.Validation;

/// <summary>
/// Validates external path strings for security (absolute, no traversal).
/// </summary>
public static class ExternalPathValidator
{
    /// <summary>
    /// Validates that a path is non-empty, absolute, and contains no path traversal sequences.
    /// </summary>
    public static ValidationResult Validate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ValidationResult.Failure("Path", "REQUIRED", "Path must not be null or empty.");
        }

        if (!Path.IsPathRooted(path))
        {
            return ValidationResult.Failure("Path", "NOT_ABSOLUTE", "Path must be an absolute path.");
        }

        if (path.Contains(".."))
        {
            return ValidationResult.Failure("Path", "PATH_TRAVERSAL", "Path must not contain '..' traversal sequences.");
        }

        return ValidationResult.Success();
    }
}
