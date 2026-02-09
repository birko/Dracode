namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Categorizes errors to determine if they are transient (retriable) or permanent
    /// </summary>
    public static class ErrorClassifier
    {
        /// <summary>
        /// Error category determining retry behavior
        /// </summary>
        public enum ErrorCategory
        {
            /// <summary>
            /// Unknown error type - treat as permanent by default
            /// </summary>
            Unknown,
            
            /// <summary>
            /// Transient errors that can be retried (network, timeout, rate limit)
            /// </summary>
            Transient,
            
            /// <summary>
            /// Permanent errors that should not be retried (syntax, configuration)
            /// </summary>
            Permanent
        }

        private static readonly string[] TransientErrorPatterns = new[]
        {
            // Network errors
            "network error",
            "connection timeout",
            "connection reset",
            "unable to connect",
            "could not connect",
            "timed out",
            "timeout",
            "no connection",
            "connection refused",
            "connection failed",
            "socket error",
            "host unreachable",
            "network unreachable",
            
            // HTTP errors
            "429",
            "rate limit",
            "too many requests",
            "503",
            "service unavailable",
            "502",
            "bad gateway",
            "504",
            "gateway timeout",
            "500",
            "internal server error",
            
            // Provider-specific transient errors
            "quota exceeded",
            "overloaded",
            "capacity exceeded",
            "try again later",
            "temporarily unavailable",
            "throttled"
        };

        private static readonly string[] PermanentErrorPatterns = new[]
        {
            // Authentication & Authorization
            "401",
            "unauthorized",
            "authentication failed",
            "invalid api key",
            "invalid token",
            "403",
            "forbidden",
            "access denied",
            "permission denied",
            
            // Configuration errors
            "invalid configuration",
            "invalid model",
            "model not found",
            "invalid parameter",
            "invalid request",
            "bad request",
            "400",
            
            // Syntax & Validation errors
            "syntax error",
            "parse error",
            "validation error",
            "invalid json",
            "invalid format",
            "schema violation",
            
            // Resource not found
            "404",
            "not found",
            "does not exist",
            "resource not found",
            
            // Content policy violations
            "content policy",
            "content filter",
            "safety violation",
            "blocked by policy"
        };

        /// <summary>
        /// Classifies an error message into a category
        /// </summary>
        /// <param name="errorMessage">Error message to classify</param>
        /// <returns>Error category</returns>
        public static ErrorCategory Classify(string? errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return ErrorCategory.Unknown;
            }

            var lowerError = errorMessage.ToLowerInvariant();

            // Check transient patterns first (more common, prioritize retries)
            foreach (var pattern in TransientErrorPatterns)
            {
                if (lowerError.Contains(pattern))
                {
                    return ErrorCategory.Transient;
                }
            }

            // Check permanent patterns
            foreach (var pattern in PermanentErrorPatterns)
            {
                if (lowerError.Contains(pattern))
                {
                    return ErrorCategory.Permanent;
                }
            }

            // Default to permanent for safety (avoid infinite retries on unknown errors)
            return ErrorCategory.Permanent;
        }

        /// <summary>
        /// Checks if an error is retriable
        /// </summary>
        /// <param name="errorMessage">Error message to check</param>
        /// <returns>True if error should be retried</returns>
        public static bool IsTransient(string? errorMessage)
        {
            return Classify(errorMessage) == ErrorCategory.Transient;
        }

        /// <summary>
        /// Checks if an error is permanent and should not be retried
        /// </summary>
        /// <param name="errorMessage">Error message to check</param>
        /// <returns>True if error should not be retried</returns>
        public static bool IsPermanent(string? errorMessage)
        {
            return Classify(errorMessage) == ErrorCategory.Permanent;
        }
    }
}
