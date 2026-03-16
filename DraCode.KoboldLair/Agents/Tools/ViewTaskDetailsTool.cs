using System.Text;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for viewing detailed task information including errors, plan progress, dependencies, and output files.
    /// </summary>
    public class ViewTaskDetailsTool : Tool
    {
        private readonly DrakeFactory? _drakeFactory;
        private readonly ProjectService _projectService;
        private readonly KoboldPlanService? _planService;

        public ViewTaskDetailsTool(DrakeFactory? drakeFactory, ProjectService projectService, KoboldPlanService? planService = null)
        {
            _drakeFactory = drakeFactory;
            _projectService = projectService;
            _planService = planService;
        }

        public override string Name => "view_task_details";

        public override string Description =>
            "View detailed information about tasks in a project. Actions: 'list' shows all tasks with status/priority/dependencies, " +
            "'detail' shows full details for a specific task including error messages and plan step progress, " +
            "'plan' shows the implementation plan steps for a task.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'list' (all tasks in project), 'detail' (single task details), 'plan' (task implementation plan steps)",
                    @enum = new[] { "list", "detail", "plan" }
                },
                project = new
                {
                    type = "string",
                    description = "Project ID or name"
                },
                task_id = new
                {
                    type = "string",
                    description = "Task ID (required for 'detail' and 'plan' actions)"
                },
                status_filter = new
                {
                    type = "string",
                    description = "Optional: filter tasks by status (for 'list' action)",
                    @enum = new[] { "all", "unassigned", "working", "done", "failed", "blocked" }
                },
                show_areas = new
                {
                    type = "boolean",
                    description = "Optional: show task breakdown by area (for 'list' action)"
                }
            },
            required = new[] { "action", "project" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var actionVal)
                ? actionVal?.ToString()?.ToLowerInvariant()
                : "list";

            if (!input.TryGetValue("project", out var projectVal) || string.IsNullOrEmpty(projectVal?.ToString()))
            {
                return "Error: 'project' parameter is required.";
            }

            var projectIdOrName = projectVal.ToString()!;

            return action switch
            {
                "list" => ExecuteList(projectIdOrName, input),
                "detail" => ExecuteDetail(projectIdOrName, input),
                "plan" => await ExecutePlanAsync(projectIdOrName, input),
                _ => $"Unknown action: {action}. Use 'list', 'detail', or 'plan'."
            };
        }

        private string ExecuteList(string projectIdOrName, Dictionary<string, object> input)
        {
            try
            {
                var tasks = GetTasksForProject(projectIdOrName);
                if (tasks == null || tasks.Count == 0)
                {
                    return $"No tasks found for project '{projectIdOrName}'.";
                }

                // Apply status filter
                var statusFilter = input.TryGetValue("status_filter", out var filterVal)
                    ? filterVal?.ToString()?.ToLowerInvariant()
                    : "all";

                var filtered = statusFilter switch
                {
                    "unassigned" => tasks.Where(t => t.Status == TaskStatus.Unassigned).ToList(),
                    "working" => tasks.Where(t => t.Status == TaskStatus.Working || t.Status == TaskStatus.NotInitialized).ToList(),
                    "done" => tasks.Where(t => t.Status == TaskStatus.Done).ToList(),
                    "failed" => tasks.Where(t => t.Status == TaskStatus.Failed).ToList(),
                    "blocked" => tasks.Where(t => t.Status == TaskStatus.BlockedByFailure).ToList(),
                    _ => tasks
                };

                var sb = new StringBuilder();

                // Calculate overall progress
                var total = tasks.Count;
                var done = tasks.Count(t => t.Status == TaskStatus.Done);
                var failed = tasks.Count(t => t.Status == TaskStatus.Failed);
                var working = tasks.Count(t => t.Status == TaskStatus.Working || t.Status == TaskStatus.NotInitialized);
                var blocked = tasks.Count(t => t.Status == TaskStatus.BlockedByFailure);
                var unassigned = tasks.Count(t => t.Status == TaskStatus.Unassigned);
                var percent = total > 0 ? (done * 100.0 / total) : 0;

                // Progress bar
                var barLength = 20;
                var filledLength = (int)(percent / 100 * barLength);
                var bar = new string('█', filledLength) + new string('░', barLength - filledLength);

                // Overall progress summary at the top
                sb.AppendLine($"# Project: {projectIdOrName}\n");
                sb.AppendLine($"**Overall Progress:** [{bar}] {percent:F1}% ({done}/{total} tasks completed)\n");
                sb.AppendLine($"**Task Breakdown:** ✅ {done} done | 🔨 {working} working | ⏳ {unassigned} unassigned | ❌ {failed} failed | 🚫 {blocked} blocked\n");
                sb.AppendLine($"**Filter:** Showing {filtered.Count} of {total} tasks\n");

                if (filtered.Count > 0)
                {
                    sb.AppendLine("| Status | Priority | ID | Task | Agent | Dependencies |");
                    sb.AppendLine("|--------|----------|----|------|-------|--------------|");

                    foreach (var task in filtered.OrderBy(t => t.Status).ThenByDescending(t => t.Priority))
                    {
                        var statusIcon = GetStatusIcon(task.Status);
                        var prioIcon = GetPriorityIcon(task.Priority);
                        var desc = Truncate(task.Task, 35);
                        var agent = string.IsNullOrEmpty(task.AssignedAgent) ? "-" : task.AssignedAgent;
                        var deps = task.Dependencies.Count > 0 ? string.Join(", ", task.Dependencies.Take(3)) : "-";
                        if (task.Dependencies.Count > 3) deps += $" +{task.Dependencies.Count - 3}";

                        var commitWarn = task.CommitFailed ? " ⚠️" : "";
                        sb.AppendLine($"| {statusIcon} {task.Status}{commitWarn} | {prioIcon} {task.Priority} | {task.Id[..8]} | {desc} | {agent} | {deps} |");
                    }
                }
                else
                {
                    sb.AppendLine("*No tasks match the current filter.*");
                }

                // Summary by area (if available)
                if (input.TryGetValue("show_areas", out var showAreasVal) && showAreasVal?.ToString() == "true")
                {
                    sb.AppendLine();
                    sb.AppendLine("**By Area:**");
                    var byArea = tasks.GroupBy(t => ExtractAreaFromTask(t.Task)).OrderBy(g => g.Key);
                    foreach (var area in byArea)
                    {
                        var areaDone = area.Count(t => t.Status == TaskStatus.Done);
                        var areaPercent = area.Count() > 0 ? areaDone * 100.0 / area.Count() : 0;
                        sb.AppendLine($"  - {area.Key}: {areaDone}/{area.Count()} ({areaPercent:F0}%)");
                    }
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error listing tasks: {ex.Message}";
            }
        }

        private string ExecuteDetail(string projectIdOrName, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("task_id", out var taskIdVal) || string.IsNullOrEmpty(taskIdVal?.ToString()))
            {
                return "Error: 'task_id' is required for the 'detail' action.";
            }

            try
            {
                var taskId = taskIdVal.ToString()!;
                var tasks = GetTasksForProject(projectIdOrName);
                var task = tasks?.FirstOrDefault(t => t.Id == taskId || t.Id.StartsWith(taskId));

                if (task == null)
                {
                    return $"Task '{taskId}' not found in project '{projectIdOrName}'.";
                }

                // Calculate overall project progress for context
                var total = tasks.Count;
                var done = tasks.Count(t => t.Status == TaskStatus.Done);
                var percent = total > 0 ? (done * 100.0 / total) : 0;

                var sb = new StringBuilder();
                sb.AppendLine($"# Project: {projectIdOrName}\n");
                sb.AppendLine($"**Overall Progress:** {done}/{total} tasks completed ({percent:F1}%)\n");
                sb.AppendLine($"---\n");
                sb.AppendLine($"## Task: {task.Id[..8]}");
                sb.AppendLine();
                sb.AppendLine($"**Description:** {task.Task}");
                sb.AppendLine($"**Status:** {GetStatusIcon(task.Status)} {task.Status}");
                sb.AppendLine($"**Priority:** {GetPriorityIcon(task.Priority)} {task.Priority}");
                sb.AppendLine($"**Agent:** {(string.IsNullOrEmpty(task.AssignedAgent) ? "Unassigned" : task.AssignedAgent)}");
                sb.AppendLine($"**Created:** {task.CreatedAt:yyyy-MM-dd HH:mm}");
                if (task.UpdatedAt.HasValue)
                    sb.AppendLine($"**Updated:** {task.UpdatedAt:yyyy-MM-dd HH:mm}");
                sb.AppendLine($"**Spec Version:** v{task.SpecificationVersion}");

                if (!string.IsNullOrEmpty(task.FeatureId))
                    sb.AppendLine($"**Feature:** {task.FeatureId}");

                if (!string.IsNullOrEmpty(task.Provider))
                    sb.AppendLine($"**Provider:** {task.Provider}");

                // Dependencies
                if (task.Dependencies.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## Dependencies");
                    foreach (var dep in task.Dependencies)
                        sb.AppendLine($"- {dep}");
                }

                // Error details
                if (!string.IsNullOrEmpty(task.ErrorMessage))
                {
                    sb.AppendLine();
                    sb.AppendLine("## Error");
                    sb.AppendLine($"**Category:** {task.ErrorCategory ?? "Unknown"}");
                    sb.AppendLine($"**Message:** {task.ErrorMessage}");
                    sb.AppendLine($"**Retry Count:** {task.RetryCount}");
                    if (task.NextRetryAt.HasValue)
                        sb.AppendLine($"**Next Retry:** {task.NextRetryAt:yyyy-MM-dd HH:mm:ss}");
                }

                // Output files
                if (task.OutputFiles.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## Output Files");
                    foreach (var file in task.OutputFiles)
                        sb.AppendLine($"- `{file}`");
                }

                // Commit info
                if (task.CommitFailed)
                {
                    sb.AppendLine();
                    sb.AppendLine("⚠️ **COMMIT FAILED** — Task completed but code was not committed to git.");
                    sb.AppendLine("Use `git_commit` tool to manually commit the changes, or `retry_failed_task` to re-run.");
                }
                else if (!string.IsNullOrEmpty(task.CommitSha))
                {
                    sb.AppendLine();
                    sb.AppendLine($"**Commit:** `{task.CommitSha}`");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting task details: {ex.Message}";
            }
        }

        private async Task<string> ExecutePlanAsync(string projectIdOrName, Dictionary<string, object> input)
        {
            if (!input.TryGetValue("task_id", out var taskIdVal) || string.IsNullOrEmpty(taskIdVal?.ToString()))
            {
                return "Error: 'task_id' is required for the 'plan' action.";
            }

            if (_planService == null)
            {
                return "Plan service is not available.";
            }

            try
            {
                var taskId = taskIdVal.ToString()!;
                var project = FindProject(projectIdOrName);
                if (project == null)
                    return $"Project '{projectIdOrName}' not found.";

                // Find full task ID if partial
                var tasks = GetTasksForProject(projectIdOrName);
                var task = tasks?.FirstOrDefault(t => t.Id == taskId || t.Id.StartsWith(taskId));
                if (task != null) taskId = task.Id;

                var plan = await _planService.LoadPlanAsync(project.Id, taskId);
                if (plan == null)
                {
                    return $"No implementation plan found for task '{taskId[..Math.Min(8, taskId.Length)]}' in project '{projectIdOrName}'.";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"# Plan for: {Truncate(plan.TaskDescription, 50)}");
                sb.AppendLine();
                sb.AppendLine($"**Status:** {plan.Status}");
                sb.AppendLine($"**Steps:** {plan.Steps.Count} total");
                sb.AppendLine($"**Current Step:** {plan.CurrentStepIndex}");
                sb.AppendLine($"**Created:** {plan.CreatedAt:yyyy-MM-dd HH:mm}");
                sb.AppendLine();

                // Step progress
                var completed = plan.Steps.Count(s => s.Status == StepStatus.Completed);
                var failed = plan.Steps.Count(s => s.Status == StepStatus.Failed);
                var skipped = plan.Steps.Count(s => s.Status == StepStatus.Skipped);
                var pending = plan.Steps.Count(s => s.Status == StepStatus.Pending);
                var inProgress = plan.Steps.Count(s => s.Status == StepStatus.InProgress);

                sb.AppendLine($"**Progress:** {completed}/{plan.Steps.Count} completed | {failed} failed | {skipped} skipped | {inProgress} in-progress | {pending} pending");
                sb.AppendLine();

                sb.AppendLine("## Steps");
                sb.AppendLine("| # | Status | Title | Files | Retries |");
                sb.AppendLine("|---|--------|-------|-------|---------|");

                foreach (var step in plan.Steps)
                {
                    var stepIcon = step.Status switch
                    {
                        StepStatus.Completed => "✅",
                        StepStatus.Failed => "❌",
                        StepStatus.Skipped => "⏭️",
                        StepStatus.InProgress => "🔨",
                        _ => "⏳"
                    };
                    var files = step.FilesToCreate.Count + step.FilesToModify.Count;
                    var retries = step.RetryCount > 0 ? $"{step.RetryCount}/{step.MaxRetries}" : "-";
                    sb.AppendLine($"| {step.Index} | {stepIcon} {step.Status} | {Truncate(step.Title, 35)} | {files} | {retries} |");
                }

                // Show failed step details
                var failedSteps = plan.Steps.Where(s => s.Status == StepStatus.Failed).ToList();
                if (failedSteps.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## Failed Steps");
                    foreach (var step in failedSteps)
                    {
                        sb.AppendLine($"### Step {step.Index}: {step.Title}");
                        sb.AppendLine($"**Error:** {step.LastErrorMessage ?? "Unknown"}");
                        sb.AppendLine($"**Category:** {step.ErrorCategory ?? "Unknown"}");
                        sb.AppendLine($"**Files to create:** {string.Join(", ", step.FilesToCreate.Select(f => $"`{f}`"))}");
                        sb.AppendLine($"**Files to modify:** {string.Join(", ", step.FilesToModify.Select(f => $"`{f}`"))}");
                    }
                }

                // Lessons learned
                if (plan.LessonsLearned.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("## Lessons Learned");
                    foreach (var lesson in plan.LessonsLearned)
                        sb.AppendLine($"- {lesson}");
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error loading plan: {ex.Message}";
            }
        }

        private List<TaskRecord>? GetTasksForProject(string projectIdOrName)
        {
            if (_drakeFactory == null) return null;

            var drakes = FindDrakesForProject(projectIdOrName);
            var allTasks = new List<TaskRecord>();

            foreach (var drake in drakes)
            {
                var tasks = drake.GetAllTasks();
                if (tasks != null)
                    allTasks.AddRange(tasks);
            }

            // If no in-memory drakes, try loading from files
            if (allTasks.Count == 0)
            {
                var project = FindProject(projectIdOrName);
                if (project?.Paths?.TaskFiles != null)
                {
                    foreach (var taskFile in project.Paths.TaskFiles.Values)
                    {
                        var tracker = new TaskTracker();
                        var resolved = Path.IsPathRooted(taskFile)
                            ? taskFile
                            : Path.Combine(_projectService.ProjectsPath, taskFile);
                        tracker.LoadFromFile(resolved);
                        allTasks.AddRange(tracker.GetAllTasks());
                    }
                }
            }

            return allTasks;
        }

        private List<Orchestrators.Drake> FindDrakesForProject(string projectIdOrName)
        {
            var project = FindProject(projectIdOrName);
            if (project == null) return new();
            return _drakeFactory!.GetDrakesByProject(project.Id);
        }

        private Models.Projects.Project? FindProject(string projectIdOrName)
        {
            var project = _projectService.GetProject(projectIdOrName);
            if (project == null)
            {
                // Try by name
                var all = _projectService.GetAllProjects();
                project = all.FirstOrDefault(p => p.Name.Equals(projectIdOrName, StringComparison.OrdinalIgnoreCase));
            }
            return project;
        }

        private static string GetStatusIcon(TaskStatus status) => status switch
        {
            TaskStatus.Done => "✅",
            TaskStatus.Failed => "❌",
            TaskStatus.Working => "🔨",
            TaskStatus.NotInitialized => "📋",
            TaskStatus.Unassigned => "⏳",
            TaskStatus.BlockedByFailure => "🚫",
            _ => "❓"
        };

        private static string GetPriorityIcon(TaskPriority priority) => priority switch
        {
            TaskPriority.Critical => "🔴",
            TaskPriority.High => "🟠",
            TaskPriority.Normal => "🟢",
            TaskPriority.Low => "🔵",
            _ => "⚪"
        };

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text;
            return text[..(maxLength - 3)] + "...";
        }

        /// <summary>
        /// Extracts the area name from a task description.
        /// Task format: "[frontend-1] Task name..." → "frontend"
        /// </summary>
        private static string ExtractAreaFromTask(string taskDescription)
        {
            var match = System.Text.RegularExpressions.Regex.Match(taskDescription, @"^\[([a-zA-Z]+)-\d+\]");
            return match.Success ? match.Groups[1].Value : "general";
        }
    }
}
