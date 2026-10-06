using Birko.AI.Tools;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for permanently deleting a project from the registry.
    /// Only cancelled projects can be deleted. Optionally deletes project files from disk.
    /// </summary>
    public class DeleteProjectTool : Tool
    {
        private readonly Func<string, Project?>? _getProject;
        private readonly Func<string, bool, Task<ProjectDeletionResult>>? _deleteProject;
        private readonly Func<List<(string Id, string Name)>>? _getAllProjects;

        public DeleteProjectTool(
            Func<string, Project?>? getProject = null,
            Func<string, bool, Task<ProjectDeletionResult>>? deleteProject = null,
            Func<List<(string Id, string Name)>>? getAllProjects = null)
        {
            _getProject = getProject;
            _deleteProject = deleteProject;
            _getAllProjects = getAllProjects;
        }

        public override string Name => "delete_project";

        public override string Description =>
            "Permanently delete a project from the registry. Only projects in 'Cancelled' execution state can be deleted. " +
            "Use 'cancel_project' first to cancel a project before deleting it. " +
            "Optionally deletes project files from disk (workspace, tasks, plans).";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                project = new
                {
                    type = "string",
                    description = "Project ID or name to delete"
                },
                delete_files = new
                {
                    type = "boolean",
                    description = "Also delete project files from disk (default: false)"
                },
                confirm = new
                {
                    type = "string",
                    description = "Type 'confirmed' to confirm permanent deletion",
                    @enum = new[] { "confirmed" }
                }
            },
            required = new[] { "project", "confirm" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input, CancellationToken cancellationToken = default)
        {
            if (_getProject == null || _deleteProject == null)
                return "Delete project functionality is not available.";

            if (!input.TryGetValue("project", out var projectVal) || string.IsNullOrEmpty(projectVal?.ToString()))
                return "Error: 'project' parameter is required.";

            if (!input.TryGetValue("confirm", out var confirmVal) || confirmVal?.ToString()?.ToLower() != "confirmed")
                return "Error: Confirmation required. Set confirm to 'confirmed' to permanently delete this project.";

            var projectIdOrName = projectVal.ToString()!;
            var deleteFiles = input.TryGetValue("delete_files", out var deleteFilesVal) &&
                              (deleteFilesVal?.ToString()?.ToLower() == "true" || deleteFilesVal?.ToString() == "1");

            try
            {
                // Find the project
                var project = _getProject(projectIdOrName);
                if (project == null)
                {
                    // Try finding by name
                    if (_getAllProjects != null)
                    {
                        var match = _getAllProjects().FirstOrDefault(p =>
                            p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));
                        if (match.Id != null)
                            project = _getProject(match.Id);
                    }
                }

                if (project == null)
                    return $"Project '{projectIdOrName}' not found.";

                // Only allow deleting cancelled projects
                if (project.ExecutionState != ProjectExecutionState.Cancelled)
                {
                    return $"Cannot delete project '{project.Name}': Execution state is '{project.ExecutionState}'.\n" +
                           "Only cancelled projects can be deleted. Use `cancel_project` first to cancel it.";
                }

                var projectName = project.Name;
                var projectId = project.Id;

                var result = await _deleteProject(projectId, deleteFiles);
                if (!result.Deleted)
                    return $"Failed to delete project '{projectName}': {result.Message}";

                SendMessage("success", $"Project deleted: {projectName}");

                // The message names anything that could not be removed, so the reply never claims more than happened
                return $"✅ Project '{projectName}' has been permanently deleted from the registry ({result.Message}).";
            }
            catch (Exception ex)
            {
                return $"Error deleting project: {ex.Message}";
            }
        }
    }
}
