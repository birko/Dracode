using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for manually triggering or retrying project verification.
    /// </summary>
    public class RetryVerificationTool : Tool
    {
        private readonly Func<List<(string Id, string Name, string Status, string? VerificationStatus)>>? _getProjectsNeedingVerification;
        private readonly Func<string, bool>? _retryVerification;
        private readonly Func<string, (bool Success, string? VerificationStatus, DateTime? LastVerified, string? Summary)>? _getVerificationStatus;

        public RetryVerificationTool(
            Func<List<(string Id, string Name, string Status, string? VerificationStatus)>>? getProjectsNeedingVerification,
            Func<string, bool>? retryVerification,
            Func<string, (bool Success, string? VerificationStatus, DateTime? LastVerified, string? Summary)>? getVerificationStatus)
        {
            _getProjectsNeedingVerification = getProjectsNeedingVerification;
            _retryVerification = retryVerification;
            _getVerificationStatus = getVerificationStatus;
        }

        public override string Name => "retry_verification";

        public override string Description =>
            "Manage project verification. " +
            "Use action 'list' to see projects awaiting verification or with failed verification. " +
            "Use action 'run' with a project name to trigger or retry verification. " +
            "Use action 'status' to check verification status and view summary.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'list' (show projects needing verification), 'run' (trigger verification), 'status' (check verification status)",
                    @enum = new[] { "list", "run", "status" }
                },
                project = new
                {
                    type = "string",
                    description = "Project name or ID (required for 'run' and 'status' actions)"
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var actionObj) ? actionObj?.ToString()?.ToLowerInvariant() : null;
            var project = input.TryGetValue("project", out var projObj) ? projObj?.ToString() : null;

            return action switch
            {
                "list" => ListProjectsNeedingVerification(),
                "run" => TriggerVerification(project),
                "status" => GetVerificationStatus(project),
                _ => "Unknown action. Use 'list', 'run', or 'status'."
            };
        }

        private string ListProjectsNeedingVerification()
        {
            if (_getProjectsNeedingVerification == null)
            {
                return "Error: Verification service not available.";
            }

            try
            {
                var projects = _getProjectsNeedingVerification();

                if (projects.Count == 0)
                {
                    return "✅ No projects awaiting verification.";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"## Projects Needing Verification ({projects.Count})\n");
                result.AppendLine("| Project | Status | Verification Status |");
                result.AppendLine("|---------|--------|---------------------|");

                foreach (var (id, name, status, verificationStatus) in projects)
                {
                    result.AppendLine($"| {name} | {status} | {verificationStatus ?? "NotStarted"} |");
                }

                result.AppendLine();
                result.AppendLine("**To trigger verification**, use: action='run' with project='<project name>'");
                result.AppendLine("**To view verification results**, use: action='status' with project='<project name>'");

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error listing projects: {ex.Message}";
            }
        }

        private string TriggerVerification(string? project)
        {
            if (string.IsNullOrEmpty(project))
            {
                return "Error: 'project' parameter is required for run action.";
            }

            if (_retryVerification == null)
            {
                return "Error: Verification service not available.";
            }

            try
            {
                var success = _retryVerification(project);

                if (success)
                {
                    return $"🔄 **Verification triggered for '{project}'**\n\n" +
                           "The project verification has been reset and will be processed within 30 seconds.\n\n" +
                           "Use action='status' to check verification results once complete.";
                }
                else
                {
                    return $"❌ Could not trigger verification for '{project}'.\n\n" +
                           "This can happen if:\n" +
                           "- The project doesn't exist\n" +
                           "- The project is not in 'AwaitingVerification' status\n\n" +
                           "Use action='list' to see projects eligible for verification.";
                }
            }
            catch (Exception ex)
            {
                return $"Error triggering verification: {ex.Message}";
            }
        }

        private string GetVerificationStatus(string? project)
        {
            if (string.IsNullOrEmpty(project))
            {
                return "Error: 'project' parameter is required for status action.";
            }

            if (_getVerificationStatus == null)
            {
                return "Error: Verification status service not available.";
            }

            try
            {
                var (success, verificationStatus, lastVerified, summary) = _getVerificationStatus(project);

                if (!success)
                {
                    return $"❌ Project '{project}' not found.";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"## Verification Status: {project}\n");
                result.AppendLine($"**Status:** {verificationStatus ?? "NotStarted"}");

                if (lastVerified.HasValue)
                {
                    result.AppendLine($"**Last Verified:** {lastVerified.Value:yyyy-MM-dd HH:mm:ss} UTC");
                }

                if (!string.IsNullOrEmpty(summary))
                {
                    result.AppendLine($"\n**Summary:**\n{summary}");
                }

                if (verificationStatus == "Failed")
                {
                    result.AppendLine("\n**To retry verification**, use action='run' with this project name.");
                    result.AppendLine("**To view full report**, use the 'view_verification_report' tool.");
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting verification status: {ex.Message}";
            }
        }
    }
}
