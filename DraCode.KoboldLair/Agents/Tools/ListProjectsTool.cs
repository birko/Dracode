using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for listing all registered projects
    /// </summary>
    public class ListProjectsTool : Tool
    {
        private readonly Func<List<ProjectInfo>>? _getProjects;

        public ListProjectsTool(Func<List<ProjectInfo>>? getProjects)
        {
            _getProjects = getProjects;
        }

        public override string Name => "list_projects";

        public override string Description =>
            "Lists all registered projects in KoboldLair with their status and feature counts. " +
            "Use this to show the user what projects exist and offer to continue or start new.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            if (_getProjects == null)
            {
                return "No projects found. You can create a new project by gathering requirements.";
            }

            try
            {
                var projects = _getProjects();

                if (projects.Count == 0)
                {
                    return "No projects found. This appears to be a fresh start - you can help the user create their first project!";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"**{projects.Count} project(s):**\n");
                result.AppendLine("| Status | Project | Execution | Features | Git | Updated |");
                result.AppendLine("|--------|---------|-----------|----------|-----|---------|");

                foreach (var project in projects.OrderByDescending(p => p.UpdatedAt))
                {
                    var statusIcon = project.Status switch
                    {
                        "New" => "🆕",
                        "WyvernAssigned" => "📋",
                        "WyrmAssigned" => "🔍",
                        "Analyzed" => "✅",
                        "SpecificationModified" => "📝",
                        "InProgress" => "🔨",
                        "Completed" => "🎉",
                        "Failed" => "❌",
                        _ => "❓"
                    };

                    var execIcon = project.ExecutionState switch
                    {
                        "Running" => "▶️",
                        "Paused" => "⏸️",
                        "Suspended" => "⏹️",
                        "Cancelled" => "❌",
                        _ => "▶️"
                    };

                    var gitIcon = project.HasGitRepository ? "✓" : "✗";

                    // Show pending features indicator
                    var featuresDisplay = project.FeatureCount > 0
                        ? (project.PendingFeatureCount > 0
                            ? $"{project.FeatureCount} ({project.PendingFeatureCount} 🆕 pending)"
                            : $"{project.FeatureCount}")
                        : "0";

                    // Show external paths indicator
                    var pathsIndicator = project.AllowedExternalPaths.Count > 0 ? $" 📁{project.AllowedExternalPaths.Count}" : "";

                    result.AppendLine($"| {statusIcon} {project.Status} | {project.Name}{pathsIndicator} | {execIcon} {project.ExecutionState} | {featuresDisplay} | {gitIcon} | {project.UpdatedAt:MM-dd HH:mm} |");
                }

                // Add notification if any projects have allowed external paths
                var projectsWithExternalPaths = projects.Where(p => p.AllowedExternalPaths.Count > 0).ToList();
                if (projectsWithExternalPaths.Any())
                {
                    result.AppendLine();
                    result.AppendLine("**📁 External Paths Configured:**");
                    result.AppendLine("The following projects have access to external directories:");
                    foreach (var p in projectsWithExternalPaths)
                    {
                        var pathsList = string.Join(", ", p.AllowedExternalPaths.Select(path => $"`{path}`"));
                        result.AppendLine($"- **{p.Name}**: {pathsList}");
                    }
                }

                // Add notification if any projects have pending features
                var projectsWithPending = projects.Where(p => p.PendingFeatureCount > 0).ToList();
                if (projectsWithPending.Any())
                {
                    result.AppendLine();
                    result.AppendLine("**⚠️ Draft Features Detected:**");
                    foreach (var p in projectsWithPending)
                    {
                        result.AppendLine($"- **{p.Name}**: {p.PendingFeatureCount} draft feature(s) not yet ready for processing");
                    }
                    result.AppendLine();
                    result.AppendLine("*To process draft features:*");
                    result.AppendLine("1. Use `process_features` with action 'list' to see draft features");
                    result.AppendLine("2. Use `process_features` with action 'promote' to mark features as Ready");
                    result.AppendLine("3. Use `process_features` with action 'update_spec' to trigger Wyvern analysis");
                }

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error listing projects: {ex.Message}";
            }
        }
    }
}
