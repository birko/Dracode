using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for managing external path access for projects.
    /// Allows viewing, adding, and removing paths that agents can access outside the workspace.
    /// </summary>
    public class ExternalPathTool : Tool
    {
        private readonly Func<string, IReadOnlyList<string>>? _getExternalPaths;
        private readonly Func<string, string, Task>? _addExternalPath;
        private readonly Func<string, string, Task<bool>>? _removeExternalPath;
        private readonly Func<List<(string Id, string Name)>>? _getAllProjects;

        public ExternalPathTool(
            Func<string, IReadOnlyList<string>>? getExternalPaths,
            Func<string, string, Task>? addExternalPath,
            Func<string, string, Task<bool>>? removeExternalPath,
            Func<List<(string Id, string Name)>>? getAllProjects)
        {
            _getExternalPaths = getExternalPaths;
            _addExternalPath = addExternalPath;
            _removeExternalPath = removeExternalPath;
            _getAllProjects = getAllProjects;
        }

        public override string Name => "manage_external_paths";

        public override string Description =>
            "Manage external path access for projects. " +
            "External paths allow agents to read/write files outside the project workspace. " +
            "Actions: 'list' (view allowed paths), 'add' (grant access to a path), 'remove' (revoke access).";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'list' (show allowed paths), 'add' (add new path), 'remove' (remove path)",
                    @enum = new[] { "list", "add", "remove" }
                },
                project = new
                {
                    type = "string",
                    description = "Project name or ID (required for all actions)"
                },
                path = new
                {
                    type = "string",
                    description = "External path to add or remove (required for add/remove actions)"
                }
            },
            required = new[] { "action", "project" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var actionObj) ? actionObj?.ToString()?.ToLowerInvariant() : null;
            var project = input.TryGetValue("project", out var projObj) ? projObj?.ToString() : null;
            var path = input.TryGetValue("path", out var pathObj) ? pathObj?.ToString() : null;

            if (string.IsNullOrEmpty(project))
            {
                return "Error: 'project' parameter is required.";
            }

            return action switch
            {
                "list" => ListExternalPaths(project),
                "add" => await AddExternalPathAsync(project, path),
                "remove" => await RemoveExternalPathAsync(project, path),
                _ => "Unknown action. Use 'list', 'add', or 'remove'."
            };
        }

        private string ListExternalPaths(string project)
        {
            if (_getExternalPaths == null || _getAllProjects == null)
            {
                return "External path service not available.";
            }

            try
            {
                // Resolve project ID
                var projectId = ResolveProjectId(project);
                if (projectId == null)
                {
                    return $"Error: Project '{project}' not found.";
                }

                var paths = _getExternalPaths(projectId);

                if (paths.Count == 0)
                {
                    return $"No external paths are allowed for project '{project}'.\n\n" +
                           "Agents can only access files within the project workspace.\n" +
                           "Use action 'add' to grant access to external paths.";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"## Allowed External Paths for '{project}'\n");
                result.AppendLine("| # | Path |");
                result.AppendLine("|---|------|");

                for (int i = 0; i < paths.Count; i++)
                {
                    result.AppendLine($"| {i + 1} | `{paths[i]}` |");
                }

                result.AppendLine();
                result.AppendLine("Agents can read/write files within these paths and their subdirectories.");

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error listing external paths: {ex.Message}";
            }
        }

        private async Task<string> AddExternalPathAsync(string project, string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "Error: 'path' parameter is required for 'add' action.";
            }

            if (_addExternalPath == null || _getAllProjects == null)
            {
                return "External path service not available.";
            }

            try
            {
                // Resolve project ID
                var projectId = ResolveProjectId(project);
                if (projectId == null)
                {
                    return $"Error: Project '{project}' not found.";
                }

                // Validate the path exists
                if (!Directory.Exists(path) && !File.Exists(path))
                {
                    return $"⚠️ Warning: The path '{path}' does not currently exist.\n\n" +
                           "Are you sure you want to add it? The path will be allowed but agents won't be able to access it until it's created.";
                }

                await _addExternalPath(projectId, path);

                var normalizedPath = Path.GetFullPath(path);
                return $"✅ External path access granted for project '{project}':\n\n" +
                       $"**Path:** `{normalizedPath}`\n\n" +
                       "Agents (Kobolds) working on this project can now read/write files in this location and its subdirectories.";
            }
            catch (Exception ex)
            {
                return $"Error adding external path: {ex.Message}";
            }
        }

        private async Task<string> RemoveExternalPathAsync(string project, string? path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return "Error: 'path' parameter is required for 'remove' action.";
            }

            if (_removeExternalPath == null || _getAllProjects == null)
            {
                return "External path service not available.";
            }

            try
            {
                // Resolve project ID
                var projectId = ResolveProjectId(project);
                if (projectId == null)
                {
                    return $"Error: Project '{project}' not found.";
                }

                var removed = await _removeExternalPath(projectId, path);

                if (removed)
                {
                    return $"✅ External path access revoked for project '{project}':\n\n" +
                           $"**Path:** `{path}`\n\n" +
                           "Agents can no longer access files in this location.";
                }
                else
                {
                    return $"Path '{path}' was not in the allowed list for project '{project}'.";
                }
            }
            catch (Exception ex)
            {
                return $"Error removing external path: {ex.Message}";
            }
        }

        private string? ResolveProjectId(string projectIdOrName)
        {
            if (_getAllProjects == null)
                return null;

            var projects = _getAllProjects();
            var match = projects.FirstOrDefault(p =>
                p.Id.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase) ||
                p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));

            return match.Id ?? projectIdOrName; // Return as-is if no match (might be a new project)
        }
    }
}
