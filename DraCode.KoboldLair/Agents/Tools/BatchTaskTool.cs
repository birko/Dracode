using System.Text;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for batch task operations: unblocking blocked tasks and reassigning tasks to different agent types.
    /// </summary>
    public class BatchTaskTool : Tool
    {
        private readonly DrakeFactory _drakeFactory;
        private readonly ProjectService _projectService;

        public BatchTaskTool(DrakeFactory drakeFactory, ProjectService projectService)
        {
            _drakeFactory = drakeFactory;
            _projectService = projectService;
        }

        public override string Name => "batch_task_operations";

        public override string Description => @"Batch operations on tasks. Actions:
- 'reset_blocked' - Reset all BlockedByFailure tasks in a project to Unassigned so they can be re-evaluated
- 'list_blocked' - List all blocked tasks across projects
- 'reassign' - Change the agent type for a task (e.g., switch from 'coding' to 'csharp') and reset it for execution

Examples:
- List blocked: {""action"": ""list_blocked""}
- Reset blocked: {""action"": ""reset_blocked"", ""project"": ""my-project""}
- Reassign: {""action"": ""reassign"", ""task_id"": ""abc123"", ""agent_type"": ""csharp""}";

        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'list_blocked', 'reset_blocked', 'reassign'",
                    @enum = new[] { "list_blocked", "reset_blocked", "reassign" }
                },
                project = new
                {
                    type = "string",
                    description = "Project ID or name (for 'reset_blocked')"
                },
                task_id = new
                {
                    type = "string",
                    description = "Task ID (for 'reassign')"
                },
                agent_type = new
                {
                    type = "string",
                    description = "New agent type (for 'reassign'): csharp, javascript, python, react, typescript, html, css, php, cpp, coding, documentation, test, debug, refactor, etc."
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            try
            {
                var action = input.TryGetValue("action", out var a) ? a?.ToString()?.ToLowerInvariant() : null;

                return action switch
                {
                    "list_blocked" => ListBlocked(),
                    "reset_blocked" => ResetBlocked(input),
                    "reassign" => ReassignTask(input),
                    _ => $"Error: Unknown action '{action}'. Use: list_blocked, reset_blocked, reassign"
                };
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private string ListBlocked()
        {
            var allProjects = _projectService.GetAllProjects();
            var blockedTasks = new List<(string ProjectName, string ProjectId, string TaskId, string Description, string? ErrorMessage)>();

            foreach (var project in allProjects)
            {
                var drakes = _drakeFactory.GetDrakesForProject(project.Id);

                foreach (var (drake, _) in drakes)
                {
                    foreach (var task in drake.GetAllTasks().Where(t => t.Status == TaskStatus.BlockedByFailure))
                    {
                        blockedTasks.Add((project.Name, project.Id, task.Id, task.Task, task.ErrorMessage));
                    }
                }

                if (drakes.Count == 0)
                {
                    foreach (var (area, filePath) in project.Paths.TaskFiles)
                    {
                        var tracker = new TaskTracker();
                        tracker.LoadFromFile(filePath);
                        foreach (var task in tracker.GetAllTasks().Where(t => t.Status == TaskStatus.BlockedByFailure))
                        {
                            blockedTasks.Add((project.Name, project.Id, task.Id, task.Task, task.ErrorMessage));
                        }
                    }
                }
            }

            if (blockedTasks.Count == 0)
                return "No blocked tasks found across any project.";

            var sb = new StringBuilder();
            sb.AppendLine($"Found {blockedTasks.Count} blocked task(s):\n");

            foreach (var group in blockedTasks.GroupBy(t => (t.ProjectName, t.ProjectId)))
            {
                sb.AppendLine($"**{group.Key.ProjectName}** ({group.Key.ProjectId})");
                foreach (var t in group)
                {
                    sb.AppendLine($"  - `{t.TaskId[..Math.Min(8, t.TaskId.Length)]}`: {t.Description}");
                    if (!string.IsNullOrEmpty(t.ErrorMessage))
                        sb.AppendLine($"    Blocked reason: {t.ErrorMessage}");
                }
                sb.AppendLine();
            }

            sb.AppendLine("Use `{\"action\": \"reset_blocked\", \"project\": \"<name-or-id>\"}` to unblock all tasks in a project.");
            return sb.ToString();
        }

        private string ResetBlocked(Dictionary<string, object> input)
        {
            if (!input.TryGetValue("project", out var projObj) || string.IsNullOrEmpty(projObj?.ToString()))
                return "Error: 'project' is required for 'reset_blocked'.";

            var projectIdOrName = projObj.ToString()!;
            var project = _projectService.GetProject(projectIdOrName);
            if (project == null)
                return $"Project '{projectIdOrName}' not found.";

            var drakes = _drakeFactory.GetDrakesForProject(project.Id);
            var resetCount = 0;

            if (drakes.Count > 0)
            {
                foreach (var (drake, _) in drakes)
                {
                    var blocked = drake.GetAllTasks().Where(t => t.Status == TaskStatus.BlockedByFailure).ToList();
                    foreach (var task in blocked)
                    {
                        drake.UpdateTask(task, TaskStatus.Unassigned);
                        resetCount++;
                    }
                    if (blocked.Count > 0)
                        drake.UpdateTasksFile();
                }
            }
            else
            {
                foreach (var (area, filePath) in project.Paths.TaskFiles)
                {
                    var tracker = new TaskTracker();
                    tracker.LoadFromFile(filePath);
                    var blocked = tracker.GetAllTasks().Where(t => t.Status == TaskStatus.BlockedByFailure).ToList();
                    foreach (var task in blocked)
                    {
                        tracker.UpdateTask(task, TaskStatus.Unassigned);
                        tracker.ClearError(task);
                        resetCount++;
                    }
                    if (blocked.Count > 0)
                        tracker.SaveToFile(filePath);
                }
            }

            if (resetCount == 0)
                return $"No blocked tasks found in project {project.Name}.";

            var suffix = drakes.Count == 0
                ? "Tasks will be re-evaluated when Drake starts."
                : "Drake will re-evaluate dependencies on the next cycle.";
            return $"Reset {resetCount} blocked task(s) to Unassigned in project {project.Name}.\n{suffix}";
        }

        private string ReassignTask(Dictionary<string, object> input)
        {
            if (!input.TryGetValue("task_id", out var taskIdObj) || string.IsNullOrEmpty(taskIdObj?.ToString()))
                return "Error: 'task_id' is required for 'reassign'.";
            if (!input.TryGetValue("agent_type", out var agentObj) || string.IsNullOrEmpty(agentObj?.ToString()))
                return "Error: 'agent_type' is required for 'reassign'.";

            var taskId = taskIdObj.ToString()!;
            var newAgentType = agentObj.ToString()!;

            // Validate agent type
            var normalized = AgentTypeValidator.Normalize(newAgentType);
            if (normalized == "coding" && !newAgentType.Equals("coding", StringComparison.OrdinalIgnoreCase))
            {
                // Normalize mapped it to a fallback — warn but proceed
            }

            var allProjects = _projectService.GetAllProjects();

            foreach (var project in allProjects)
            {
                // Try in-memory Drakes first
                var drakes = _drakeFactory.GetDrakesForProject(project.Id);
                foreach (var (drake, _) in drakes)
                {
                    var task = drake.GetAllTasks().FirstOrDefault(t => t.Id == taskId || t.Id.StartsWith(taskId));
                    if (task == null) continue;

                    if (task.Status == TaskStatus.Done)
                        return $"Task {taskId[..Math.Min(8, taskId.Length)]} is already Done and cannot be reassigned.";

                    var oldAgent = task.AssignedAgent;
                    task.AssignedAgent = normalized;

                    // If Working or Failed, unsummon Kobold and reset
                    if (task.Status == TaskStatus.Working || task.Status == TaskStatus.Failed)
                    {
                        var kobold = drake.GetKoboldForTask(task.Id);
                        if (kobold != null)
                            drake.UnsummonKobold(kobold.Id);
                    }

                    drake.UpdateTask(task, TaskStatus.Unassigned);
                    drake.UpdateTasksFile();

                    return $"Reassigned task `{taskId[..Math.Min(8, taskId.Length)]}` from {oldAgent} to {normalized}.\n" +
                           $"Project: {project.Name}\n" +
                           $"Task: {task.Task}\n" +
                           $"Task will be picked up by Drake on the next cycle.";
                }

                // File fallback
                if (drakes.Count == 0)
                {
                    foreach (var (area, filePath) in project.Paths.TaskFiles)
                    {
                        var tracker = new TaskTracker();
                        tracker.LoadFromFile(filePath);
                        var task = tracker.GetTaskById(taskId) ?? tracker.GetAllTasks().FirstOrDefault(t => t.Id.StartsWith(taskId));
                        if (task == null) continue;

                        if (task.Status == TaskStatus.Done)
                            return $"Task {taskId[..Math.Min(8, taskId.Length)]} is already Done and cannot be reassigned.";

                        var oldAgent = task.AssignedAgent;
                        task.AssignedAgent = normalized;
                        tracker.UpdateTask(task, TaskStatus.Unassigned);
                        tracker.ClearError(task);
                        tracker.SaveToFile(filePath);

                        return $"Reassigned task `{taskId[..Math.Min(8, taskId.Length)]}` from {oldAgent} to {normalized}.\n" +
                               $"Project: {project.Name}\n" +
                               $"Task: {task.Task}\n" +
                               $"(Reset via file — task will be picked up when Drake starts)";
                    }
                }
            }

            return $"Task {taskId[..Math.Min(8, taskId.Length)]} not found in any project.";
        }
    }
}
