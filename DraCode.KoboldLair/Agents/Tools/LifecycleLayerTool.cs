using Birko.AI.Tools;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Shows and switches a project's lifecycle layer (TASK-211, FEATURE-078 D5) for Warden: the checker agents, the
    /// person's escalation card and fix tasks for repeated failures. Only the caller's own projects.
    /// </summary>
    public class LifecycleLayerTool : Tool
    {
        private readonly Func<string, Project?> _findProject;
        private readonly Func<string, bool, Task<bool>> _setEnabled;
        private readonly Func<IReadOnlyList<(string Id, string Name)>> _visibleProjects;

        /// <param name="findProject">A project by name or id.</param>
        /// <param name="setEnabled">Switches the layer of the project with that id; false when it is gone.</param>
        /// <param name="visibleProjects">The caller's projects; any other reads as not found.</param>
        public LifecycleLayerTool(Func<string, Project?> findProject, Func<string, bool, Task<bool>> setEnabled,
            Func<IReadOnlyList<(string Id, string Name)>> visibleProjects)
        {
            _findProject = findProject;
            _setEnabled = setEnabled;
            _visibleProjects = visibleProjects;
        }

        public override string Name => "lifecycle_layer";

        public override string Description =>
            "Shows or switches a project's lifecycle layer: when on, finished tasks also go through the checker agents " +
            "(intent, correctness, comments), a task that keeps failing its checks escalates to the person instead of failing, " +
            "and repeated failures and monitor alerts become tracked fix tasks. Questions climb the agent chain either way. " +
            "New projects start with it on unless the person declined; imported and older projects start off.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new { type = "string", @enum = new[] { "status", "on", "off" }, description = "status, on or off" },
                project = new { type = "string", description = "Name or ID of the project" }
            },
            required = new[] { "action", "project" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input,
            CancellationToken cancellationToken = default)
        {
            var action = input.TryGetValue("action", out var a) ? a?.ToString()?.Trim().ToLowerInvariant() : null;
            var name = input.TryGetValue("project", out var p) ? p?.ToString()?.Trim() : null;
            if (string.IsNullOrEmpty(name))
                return "Error: project is required";

            var project = _findProject(name);
            if (project == null || !_visibleProjects().Any(v => v.Id == project.Id))
                return $"Project '{name}' not found.";

            switch (action)
            {
                case "status":
                    return $"The lifecycle layer is {(project.LifecycleEnabled ? "on" : "off")} for '{project.Name}'.";
                case "on":
                case "off":
                    var on = action == "on";
                    if (!await _setEnabled(project.Id, on))
                        return $"Project '{name}' not found.";
                    return on
                        ? $"The lifecycle layer is now on for '{project.Name}': finished tasks also go through the checker agents from the next check."
                        : $"The lifecycle layer is now off for '{project.Name}': finished tasks get only the acceptance check, as before. Nothing recorded is removed.";
                default:
                    return "Error: action must be status, on or off";
            }
        }
    }
}
