using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for retrying failed Wyvern analysis on projects.
    /// Allows the user to reset a failed project and trigger reanalysis.
    /// </summary>
    public class RetryAnalysisTool : Tool
    {
        private readonly Func<string, (bool Success, string? ErrorMessage, string? Status)>? _getProjectStatus;
        private readonly Func<string, bool>? _retryAnalysis;
        private readonly Func<List<(string Id, string Name, string Status, string? ErrorMessage)>>? _getFailedProjects;

        /// <summary>
        /// Creates a new RetryAnalysisTool
        /// </summary>
        /// <param name="getProjectStatus">Function to get project status by ID or name (returns success, errorMessage, status)</param>
        /// <param name="retryAnalysis">Function to retry analysis (returns true if successful)</param>
        /// <param name="getFailedProjects">Function to get list of failed projects</param>
        public RetryAnalysisTool(
            Func<string, (bool Success, string? ErrorMessage, string? Status)>? getProjectStatus,
            Func<string, bool>? retryAnalysis,
            Func<List<(string Id, string Name, string Status, string? ErrorMessage)>>? getFailedProjects)
        {
            _getProjectStatus = getProjectStatus;
            _retryAnalysis = retryAnalysis;
            _getFailedProjects = getFailedProjects;
        }

        public override string Name => "retry_analysis";

        public override string Description =>
            "View failed projects and retry Wyvern analysis. " +
            "Use action 'list' to see projects with failed analysis and their error messages. " +
            "Use action 'retry' with a project name to reset the project and trigger reanalysis. " +
            "Use action 'status' to check a specific project's status.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'list' (show failed projects), 'retry' (retry analysis), 'status' (check project status)",
                    @enum = new[] { "list", "retry", "status" }
                },
                project = new
                {
                    type = "string",
                    description = "Project name or ID (required for 'retry' and 'status' actions)"
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
                "list" => ListFailedProjects(),
                "retry" => RetryProject(project),
                "status" => GetProjectStatus(project),
                _ => "Unknown action. Use 'list', 'retry', or 'status'."
            };
        }

        private string ListFailedProjects()
        {
            if (_getFailedProjects == null)
            {
                return "Error: Failed projects service not available.";
            }

            try
            {
                var failedProjects = _getFailedProjects();

                if (failedProjects.Count == 0)
                {
                    return "✅ No failed projects found. All projects are processing normally.";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"## Failed Projects ({failedProjects.Count})\n");
                result.AppendLine("| Project | Status | Error |");
                result.AppendLine("|---------|--------|-------|");

                foreach (var (id, name, status, errorMessage) in failedProjects)
                {
                    var truncatedError = errorMessage?.Length > 60
                        ? errorMessage[..57] + "..."
                        : errorMessage ?? "No error message";
                    result.AppendLine($"| {name} | {status} | {truncatedError} |");
                }

                result.AppendLine();
                result.AppendLine("**To retry analysis**, use: `retry_analysis` with action='retry' and project='<project name>'");
                result.AppendLine("**To see full error**, use: `retry_analysis` with action='status' and project='<project name>'");

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error listing failed projects: {ex.Message}";
            }
        }

        private string RetryProject(string? project)
        {
            if (string.IsNullOrEmpty(project))
            {
                return "Error: 'project' parameter is required for retry action.";
            }

            if (_retryAnalysis == null)
            {
                return "Error: Retry service not available.";
            }

            try
            {
                var success = _retryAnalysis(project);

                if (success)
                {
                    return $"🔄 **Retry initiated for '{project}'**\n\n" +
                           "The project has been reset to 'New' status. " +
                           "Wyvern will pick it up and retry analysis within the next 60 seconds.\n\n" +
                           "You can check the status later with action='status'.";
                }
                else
                {
                    return $"❌ Could not retry analysis for '{project}'.\n\n" +
                           "This can happen if:\n" +
                           "- The project doesn't exist\n" +
                           "- The project is not in 'Failed' status\n\n" +
                           "Use action='list' to see failed projects, or action='status' to check this project's current state.";
                }
            }
            catch (Exception ex)
            {
                return $"Error retrying analysis: {ex.Message}";
            }
        }

        private string GetProjectStatus(string? project)
        {
            if (string.IsNullOrEmpty(project))
            {
                return "Error: 'project' parameter is required for status action.";
            }

            if (_getProjectStatus == null)
            {
                return "Error: Project status service not available.";
            }

            try
            {
                var (success, errorMessage, status) = _getProjectStatus(project);

                if (!success)
                {
                    return $"❌ Project '{project}' not found.";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"## Project Status: {project}\n");
                result.AppendLine($"**Status:** {status}");

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    result.AppendLine($"\n**Error Message:**\n```\n{errorMessage}\n```");

                    if (status == "Failed")
                    {
                        result.AppendLine("\n**To retry**, use action='retry' with this project name.");
                    }
                }
                else if (status == "Failed")
                {
                    result.AppendLine("\n⚠️ Project is in Failed status but no error message recorded.");
                    result.AppendLine("**To retry**, use action='retry' with this project name.");
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting project status: {ex.Message}";
            }
        }
    }
}
