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
                result.AppendLine($"Found {projects.Count} project(s):\n");

                foreach (var project in projects.OrderByDescending(p => p.UpdatedAt))
                {
                    var statusIcon = project.Status switch
                    {
                        "New" => "🆕",
                        "WyvernAssigned" => "📋",
                        "Analyzed" => "✅",
                        "SpecificationModified" => "📝",
                        "InProgress" => "🔨",
                        "Completed" => "🎉",
                        "Failed" => "❌",
                        _ => "❓"
                    };

                    result.AppendLine($"{statusIcon} **{project.Name}**");
                    result.AppendLine($"   Status: {project.Status}");
                    result.AppendLine($"   Features: {project.FeatureCount}");
                    result.AppendLine($"   Last Updated: {project.UpdatedAt:yyyy-MM-dd HH:mm}");
                    result.AppendLine();
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
