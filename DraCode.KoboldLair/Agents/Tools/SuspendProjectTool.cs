using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for suspending project execution long-term
    /// </summary>
    public class SuspendProjectTool : Tool
    {
        private readonly Func<string, ProjectExecutionState, bool>? _setExecutionState;

        public SuspendProjectTool(Func<string, ProjectExecutionState, bool>? setExecutionState)
        {
            _setExecutionState = setExecutionState;
        }

        public override string Name => "suspend_project";

        public override string Description =>
            "Suspends a project for long-term hold, requiring explicit resume action. " +
            "Use for projects awaiting external changes or extended delays. " +
            "Differs from pause (short-term) - indicates project won't resume soon.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                project_name = new
                {
                    type = "string",
                    description = "Name or ID of the project to suspend"
                },
                reason = new
                {
                    type = "string",
                    description = "Optional reason for suspension (e.g., 'awaiting API access', 'pending requirements')"
                }
            },
            required = new[] { "project_name" }
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

            var projectName = nameObj.ToString() ?? "";
            var reason = input.TryGetValue("reason", out var reasonObj) ? reasonObj.ToString() : null;

            try
            {
                var success = _setExecutionState(projectName, ProjectExecutionState.Suspended);
                if (success)
                {
                    var message = $"⏹️ Project '{projectName}' has been suspended.\n\n" +
                                  "This is a long-term hold. ";
                    
                    if (!string.IsNullOrEmpty(reason))
                    {
                        message += $"Reason: {reason}\n\n";
                    }
                    
                    message += "Use 'resume_project' when ready to continue.";
                    return message;
                }
                else
                {
                    return $"Error: Could not suspend project '{projectName}'. " +
                           "Make sure the project exists.";
                }
            }
            catch (Exception ex)
            {
                return $"Error suspending project: {ex.Message}";
            }
        }
    }
}
