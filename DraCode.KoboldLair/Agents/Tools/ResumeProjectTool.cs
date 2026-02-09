using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for resuming a paused or suspended project
    /// </summary>
    public class ResumeProjectTool : Tool
    {
        private readonly Func<string, ProjectExecutionState, bool>? _setExecutionState;

        public ResumeProjectTool(Func<string, ProjectExecutionState, bool>? setExecutionState)
        {
            _setExecutionState = setExecutionState;
        }

        public override string Name => "resume_project";

        public override string Description =>
            "Resumes a paused or suspended project, allowing Drake to continue processing tasks. " +
            "Cannot resume cancelled projects - they must be recreated.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                project_name = new
                {
                    type = "string",
                    description = "Name or ID of the project to resume"
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

            try
            {
                var success = _setExecutionState(projectName, ProjectExecutionState.Running);
                if (success)
                {
                    return $"▶️ Project '{projectName}' has been resumed.\n\n" +
                           "Drake will continue processing tasks in the next execution cycle (within 30 seconds).";
                }
                else
                {
                    return $"Error: Could not resume project '{projectName}'. " +
                           "Make sure the project exists and is not cancelled. " +
                           "Cancelled projects cannot be resumed.";
                }
            }
            catch (Exception ex)
            {
                return $"Error resuming project: {ex.Message}";
            }
        }
    }
}
