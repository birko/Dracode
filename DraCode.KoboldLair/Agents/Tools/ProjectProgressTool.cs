using System.Text;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for viewing project progress analytics: completion %, task breakdown, success rates, and area progress.
    /// </summary>
    public class ProjectProgressTool : Tool
    {
        private readonly DrakeFactory? _drakeFactory;
        private readonly ProjectService _projectService;

        public ProjectProgressTool(DrakeFactory? drakeFactory, ProjectService projectService)
        {
            _drakeFactory = drakeFactory;
            _projectService = projectService;
        }

        public override string Name => "project_progress";

        public override string Description =>
            "View project progress analytics: overall completion percentage, task breakdown by status, area progress, " +
            "success/failure rates, and execution timeline. Use 'overview' for a single project or 'all' for cross-project summary.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'overview' (single project progress), 'all' (cross-project summary)",
                    @enum = new[] { "overview", "all" }
                },
                project = new
                {
                    type = "string",
                    description = "Project ID or name (required for 'overview')"
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var actionVal)
                ? actionVal?.ToString()?.ToLowerInvariant()
                : "overview";

            return action switch
            {
                "overview" => ExecuteOverview(input),
                "all" => ExecuteAll(),
                _ => $"Unknown action: {action}. Use 'overview' or 'all'."
            };
        }

        private string ExecuteOverview(Dictionary<string, object> input)
        {
            if (!input.TryGetValue("project", out var projectVal) || string.IsNullOrEmpty(projectVal?.ToString()))
            {
                return "Error: 'project' parameter is required for 'overview'.";
            }

            try
            {
                var projectIdOrName = projectVal.ToString()!;
                var project = FindProject(projectIdOrName);
                if (project == null)
                    return $"Project '{projectIdOrName}' not found.";

                var tasks = GetTasksForProject(project);
                if (tasks.Count == 0)
                    return $"No tasks found for project '{project.Name}'. Project may not be analyzed yet.";

                var sb = new StringBuilder();
                sb.AppendLine($"# Project Progress: {project.Name}");
                sb.AppendLine();

                // Overall stats
                var total = tasks.Count;
                var done = tasks.Count(t => t.Status == TaskStatus.Done);
                var failed = tasks.Count(t => t.Status == TaskStatus.Failed);
                var working = tasks.Count(t => t.Status == TaskStatus.Working || t.Status == TaskStatus.NotInitialized);
                var blocked = tasks.Count(t => t.Status == TaskStatus.BlockedByFailure);
                var unassigned = tasks.Count(t => t.Status == TaskStatus.Unassigned);
                var percent = total > 0 ? (done * 100.0 / total) : 0;

                sb.AppendLine($"**Status:** {project.Status} | **Execution:** {project.ExecutionState}");
                sb.AppendLine();

                // Progress bar
                var barLength = 20;
                var filledLength = (int)(percent / 100 * barLength);
                var bar = new string('█', filledLength) + new string('░', barLength - filledLength);
                sb.AppendLine($"**Progress:** [{bar}] {percent:F1}% ({done}/{total} tasks)");
                sb.AppendLine();

                sb.AppendLine("## Task Breakdown");
                sb.AppendLine($"- ✅ Done: {done}");
                sb.AppendLine($"- 🔨 Working: {working}");
                sb.AppendLine($"- ⏳ Unassigned: {unassigned}");
                sb.AppendLine($"- ❌ Failed: {failed}");
                sb.AppendLine($"- 🚫 Blocked: {blocked}");

                // Success rate
                var attempted = done + failed;
                if (attempted > 0)
                {
                    var successRate = done * 100.0 / attempted;
                    sb.AppendLine();
                    sb.AppendLine($"**Success Rate:** {successRate:F0}% ({done}/{attempted} attempted)");
                }

                // Priority breakdown
                var byPriority = tasks.GroupBy(t => t.Priority).OrderByDescending(g => g.Key).ToList();
                if (byPriority.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## By Priority");
                    sb.AppendLine("| Priority | Total | Done | Working | Failed |");
                    sb.AppendLine("|----------|-------|------|---------|--------|");
                    foreach (var group in byPriority)
                    {
                        var gDone = group.Count(t => t.Status == TaskStatus.Done);
                        var gWorking = group.Count(t => t.Status == TaskStatus.Working || t.Status == TaskStatus.NotInitialized);
                        var gFailed = group.Count(t => t.Status == TaskStatus.Failed);
                        sb.AppendLine($"| {group.Key} | {group.Count()} | {gDone} | {gWorking} | {gFailed} |");
                    }
                }

                // Area breakdown (from task files)
                if (project.Paths?.TaskFiles != null && project.Paths.TaskFiles.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## By Area");
                    sb.AppendLine("| Area | Total | Done | % |");
                    sb.AppendLine("|------|-------|------|---|");

                    foreach (var kvp in project.Paths.TaskFiles)
                    {
                        var areaTasks = LoadAreaTasks(kvp.Value);
                        if (areaTasks.Count == 0) continue;
                        var areaDone = areaTasks.Count(t => t.Status == TaskStatus.Done);
                        var areaPercent = areaTasks.Count > 0 ? areaDone * 100.0 / areaTasks.Count : 0;
                        sb.AppendLine($"| {kvp.Key} | {areaTasks.Count} | {areaDone} | {areaPercent:F0}% |");
                    }
                }

                // Timeline
                sb.AppendLine();
                sb.AppendLine("## Timeline");
                if (project.Timestamps.CreatedAt != default)
                    sb.AppendLine($"- **Created:** {project.Timestamps.CreatedAt:yyyy-MM-dd HH:mm}");
                if (project.Timestamps.AnalyzedAt.HasValue)
                    sb.AppendLine($"- **Analyzed:** {project.Timestamps.AnalyzedAt:yyyy-MM-dd HH:mm}");
                if (project.Timestamps.LastProcessedAt.HasValue)
                    sb.AppendLine($"- **Last Processed:** {project.Timestamps.LastProcessedAt:yyyy-MM-dd HH:mm}");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting project progress: {ex.Message}";
            }
        }

        private string ExecuteAll()
        {
            try
            {
                var projects = _projectService.GetAllProjects();
                if (projects.Count == 0)
                    return "No projects found.";

                var sb = new StringBuilder();
                sb.AppendLine($"# All Projects Progress ({projects.Count} projects)\n");
                sb.AppendLine("| Project | Status | Exec | Tasks | Done | Failed | Progress |");
                sb.AppendLine("|---------|--------|------|-------|------|--------|----------|");

                var totalTasks = 0;
                var totalDone = 0;
                var totalFailed = 0;

                foreach (var project in projects.OrderByDescending(p => p.Timestamps.UpdatedAt))
                {
                    var tasks = GetTasksForProject(project);
                    var done = tasks.Count(t => t.Status == TaskStatus.Done);
                    var failed = tasks.Count(t => t.Status == TaskStatus.Failed);
                    var percent = tasks.Count > 0 ? (done * 100.0 / tasks.Count) : 0;

                    totalTasks += tasks.Count;
                    totalDone += done;
                    totalFailed += failed;

                    var progressBar = tasks.Count > 0
                        ? $"{'█'.ToString().PadRight((int)(percent / 10), '█')}{'░'.ToString().PadRight(10 - (int)(percent / 10), '░')} {percent:F0}%"
                        : "N/A";

                    sb.AppendLine($"| {Truncate(project.Name, 18)} | {project.Status} | {project.ExecutionState} | {tasks.Count} | {done} | {failed} | {progressBar} |");
                }

                // Totals
                var totalPercent = totalTasks > 0 ? (totalDone * 100.0 / totalTasks) : 0;
                sb.AppendLine();
                sb.AppendLine($"**Totals:** {totalTasks} tasks, {totalDone} done ({totalPercent:F0}%), {totalFailed} failed");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting project summary: {ex.Message}";
            }
        }

        private List<TaskRecord> GetTasksForProject(Models.Projects.Project project)
        {
            var allTasks = new List<TaskRecord>();

            // Try in-memory drakes first
            if (_drakeFactory != null)
            {
                var drakes = _drakeFactory.GetDrakesByProject(project.Id);
                foreach (var drake in drakes)
                {
                    var tasks = drake.GetAllTasks();
                    if (tasks != null) allTasks.AddRange(tasks);
                }
            }

            // Fall back to file-based loading
            if (allTasks.Count == 0 && project.Paths?.TaskFiles != null)
            {
                foreach (var taskFile in project.Paths.TaskFiles.Values)
                {
                    allTasks.AddRange(LoadAreaTasks(taskFile));
                }
            }

            return allTasks;
        }

        private List<TaskRecord> LoadAreaTasks(string taskFile)
        {
            var tracker = new TaskTracker();
            var resolved = Path.IsPathRooted(taskFile)
                ? taskFile
                : Path.Combine(_projectService.ProjectsPath, taskFile);
            try
            {
                tracker.LoadFromFile(resolved);
                return tracker.GetAllTasks();
            }
            catch
            {
                return new();
            }
        }

        private Models.Projects.Project? FindProject(string projectIdOrName)
        {
            var project = _projectService.GetProject(projectIdOrName);
            if (project == null)
            {
                var all = _projectService.GetAllProjects();
                project = all.FirstOrDefault(p => p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));
            }
            return project;
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text[..(maxLength - 3)] + "...";
        }
    }
}
