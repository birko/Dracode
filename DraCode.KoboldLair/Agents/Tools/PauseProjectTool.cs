using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for pausing project execution temporarily
    /// </summary>
    public class PauseProjectTool : Tool
    {
        private readonly Func<string, ProjectExecutionState, bool>? _setExecutionState;

        public PauseProjectTool(Func<string, ProjectExecutionState, bool>? setExecutionState)
        {
            _setExecutionState = setExecutionState;
        }

        public override string Name => "pause_project";

        public override string Description =>
            "Pauses a project temporarily, stopping Drake from processing tasks. " +
            "Use during high system load, debugging, or brief interruptions. " +
            "The project can be resumed at any time with resume_project.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                project_name = new
                {
                    type = "string",
                    description = "Name or ID of the project to pause"
                },
                reason = new
                {
                    type = "string",
                    description = "Optional reason for pausing (e.g., 'high CPU usage', 'debugging issue')"
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
                var success = _setExecutionState(projectName, ProjectExecutionState.Paused);
                if (success)
                {
                    var message = $"⏸️ Project '{projectName}' has been paused.\n\n" +
                                  "Task execution has been temporarily halted. ";
                    
                    if (!string.IsNullOrEmpty(reason))
                    {
                        message += $"Reason: {reason}\n\n";
                    }
                    
                    message += "Use 'resume_project' when ready to continue.";
                    return message;
                }
                else
                {
                    return $"Error: Could not pause project '{projectName}'. " +
                           "Make sure the project exists and is not already paused or cancelled.";
                }
            }
            catch (Exception ex)
            {
                return $"Error pausing project: {ex.Message}";
            }
        }
    }
}
