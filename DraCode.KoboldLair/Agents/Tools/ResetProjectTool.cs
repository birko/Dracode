using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for resetting a project to its initial state (Prototype).
    /// Clears all analysis, tasks, plans, workspace, and specification so
    /// the project can be restarted from scratch with a new specification.
    /// Requires explicit confirmation.
    /// </summary>
    public class ResetProjectTool : Tool
    {
        private readonly Func<string, bool, Task<(bool Success, string Message)>>? _resetProject;

        public ResetProjectTool(Func<string, bool, Task<(bool Success, string Message)>>? resetProject)
        {
            _resetProject = resetProject;
        }

        public override string Name => "reset_project";

        public override string Description =>
            "Resets a project to its initial state so it can be reprocessed from the beginning. " +
            "Stops all active agents, clears analysis, tasks, plans, and workspace. " +
            "The specification is preserved — Wyrm and Wyvern will re-analyze it. Requires confirmation.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                project_name = new
                {
                    type = "string",
                    description = "Name of the project to reset"
                },
                confirm = new
                {
                    type = "string",
                    description = "Type 'yes' to confirm the reset. This action cannot be undone.",
                    @enum = new[] { "yes" }
                },
                keep_history = new
                {
                    type = "boolean",
                    description = "If true, preserves Dragon conversation history (default: false)"
                }
            },
            required = new[] { "project_name", "confirm" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            if (_resetProject == null)
                return "Error: Reset functionality not available.";

            if (!input.TryGetValue("project_name", out var nameObj))
                return "Error: project_name is required.";

            if (!input.TryGetValue("confirm", out var confirmObj) || confirmObj?.ToString()?.ToLower() != "yes")
                return "Error: Confirmation required. Set confirm to 'yes' to reset the project. " +
                       "⚠️ This will delete all generated content (analysis, tasks, plans, workspace). Specification is preserved.";

            var projectName = nameObj.ToString() ?? "";
            var keepHistory = input.TryGetValue("keep_history", out var keepObj) &&
                              (keepObj is bool b ? b : keepObj?.ToString()?.ToLower() == "true");

            var (success, message) = await _resetProject(projectName, keepHistory);

            if (success)
                SendMessage("success", $"Project reset: {projectName}");

            return message;
        }
    }
}
