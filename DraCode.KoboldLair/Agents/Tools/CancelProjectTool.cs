using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for permanently cancelling project execution
    /// </summary>
    public class CancelProjectTool : Tool
    {
        private readonly Func<string, ProjectExecutionState, bool>? _setExecutionState;

        public CancelProjectTool(Func<string, ProjectExecutionState, bool>? setExecutionState)
        {
            _setExecutionState = setExecutionState;
        }

        public override string Name => "cancel_project";

        public override string Description =>
            "Permanently cancels a project. This is a terminal state - cancelled projects cannot be resumed. " +
            "Use when a project is abandoned or no longer needed. " +
            "IMPORTANT: This action cannot be undone - confirm with the user before cancelling.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                project_name = new
                {
                    type = "string",
                    description = "Name or ID of the project to cancel"
                },
                confirmation = new
                {
                    type = "string",
                    description = "User's confirmation (must be 'yes' or 'confirmed')",
                    @enum = new[] { "yes", "confirmed" }
                }
            },
            required = new[] { "project_name", "confirmation" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            if (_setExecutionState == null)
            {
                return "Error: Execution state control is not configured.";
            }

            if (!input.TryGetValue("project_name", out var nameObj))
            {
                return "Error: project_name is required";
            }

            if (!input.TryGetValue("confirmation", out var confirmObj))
            {
                return "Error: confirmation is required. Ask the user to confirm cancellation first.";
            }

            var projectName = nameObj.ToString() ?? "";
            var confirmation = confirmObj.ToString()?.ToLowerInvariant() ?? "";

            // Validate confirmation
            var validConfirmations = new[] { "yes", "confirmed" };
            if (!validConfirmations.Contains(confirmation))
            {
                return $"Error: Invalid confirmation '{confirmation}'. User must explicitly confirm cancellation.";
            }

            try
            {
                var success = _setExecutionState(projectName, ProjectExecutionState.Cancelled);
                if (success)
                {
                    return $"❌ Project '{projectName}' has been cancelled.\n\n" +
                           "This is a permanent action - the project will not be processed again. " +
                           "The project files remain on disk but execution is permanently disabled.";
                }
                else
                {
                    return $"Error: Could not cancel project '{projectName}'. " +
                           "Make sure the project exists.";
                }
            }
            catch (Exception ex)
            {
                return $"Error cancelling project: {ex.Message}";
            }
        }
    }
}
