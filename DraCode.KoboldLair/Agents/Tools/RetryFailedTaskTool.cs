using DraCode.Agent.Tools;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using System.Text.Json;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for retrying failed tasks. Resets failed tasks to Unassigned status
    /// so they can be picked up by Drake for execution again.
    /// </summary>
    public class RetryFailedTaskTool : Tool
    {
        private readonly DrakeFactory _drakeFactory;
        private readonly ProjectService _projectService;

        public RetryFailedTaskTool(DrakeFactory drakeFactory, ProjectService projectService)
        {
            _drakeFactory = drakeFactory;
            _projectService = projectService;
        }

        public override string Name => "retry_failed_task";

        public override string Description => @"Retries a failed task by resetting its status to Unassigned.
Use this when a task has failed and you want to retry it after resolving the underlying issue.
The task will be picked up by Drake on the next execution cycle.

Actions:
- 'list' - Lists all failed tasks across all projects
- 'retry' - Retries a specific task by task ID
- 'retry_all' - Retries all failed tasks for a specific project

Examples:
- List all failed tasks: {""action"": ""list""}
- Retry a specific task: {""action"": ""retry"", ""task_id"": ""abc123""}
- Retry all failed tasks in a project: {""action"": ""retry_all"", ""project_id"": ""proj-guid""}";

        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'list', 'retry', or 'retry_all'",
                    @enum = new[] { "list", "retry", "retry_all" }
                },
                task_id = new
                {
                    type = "string",
                    description = "Task ID to retry (required for 'retry' action)"
                },
                project_id = new
                {
                    type = "string",
                    description = "Project ID (required for 'retry_all' action)"
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> arguments)
        {
            try
            {
                if (!arguments.TryGetValue("action", out var actionObj))
                {
                    return "Error: Missing required 'action' parameter";
                }

                var action = actionObj.ToString()?.ToLowerInvariant();

                return action switch
                {
                    "list" => ListFailedTasks(),
                    "retry" => RetryTask(arguments),
                    "retry_all" => RetryAllProjectTasks(arguments),
                    _ => $"Error: Unknown action '{action}'. Use 'list', 'retry', or 'retry_all'"
                };
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private string ListFailedTasks()
        {
            var allProjects = _projectService.GetAllProjects();
            var failedTasks = new List<(string ProjectName, string ProjectId, string TaskId, string Description, string? ErrorMessage)>();
            var seenTaskIds = new HashSet<string>();

            foreach (var project in allProjects)
            {
                // Try Drakes first (in-memory, available when server has been running)
                var drakes = _drakeFactory.GetDrakesForProject(project.Id);

                foreach (var (drake, drakeName) in drakes)
                {
                    var tasks = drake.GetAllTasks().Where(t => t.Status == TaskStatus.Failed);

                    foreach (var task in tasks)
                    {
                        seenTaskIds.Add(task.Id);
                        var kobold = drake.GetKoboldForTask(task.Id);
                        var errorMsg = kobold?.ErrorMessage ?? task.ErrorMessage;

                        failedTasks.Add((
                            project.Name,
                            project.Id,
                            task.Id,
                            task.Task,
                            errorMsg
                        ));
                    }
                }

                // Fall back to file access if no Drakes found (e.g., after server restart)
                if (drakes.Count == 0)
                {
                    foreach (var (area, filePath) in project.Paths.TaskFiles)
                    {
                        var tracker = new TaskTracker();
                        tracker.LoadFromFile(filePath);

                        foreach (var task in tracker.GetAllTasks().Where(t => t.Status == TaskStatus.Failed))
                        {
                            if (seenTaskIds.Add(task.Id))
                            {
                                failedTasks.Add((
                                    project.Name,
                                    project.Id,
                                    task.Id,
                                    task.Task,
                                    task.ErrorMessage
                                ));
                            }
                        }
                    }
                }
            }

            if (failedTasks.Count == 0)
            {
                return "✅ No failed tasks found across all projects.";
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine($"❌ Found {failedTasks.Count} failed task(s):");
            result.AppendLine();

            var groupedByProject = failedTasks.GroupBy(t => (t.ProjectName, t.ProjectId));

            foreach (var projectGroup in groupedByProject)
            {
                result.AppendLine($"**Project: {projectGroup.Key.ProjectName}** (ID: {projectGroup.Key.ProjectId})");

                foreach (var task in projectGroup)
                {
                    result.AppendLine($"  - Task ID: `{task.TaskId[..Math.Min(8, task.TaskId.Length)]}`");
                    result.AppendLine($"    Description: {task.Description}");
                    if (!string.IsNullOrEmpty(task.ErrorMessage))
                    {
                        result.AppendLine($"    Error: {task.ErrorMessage}");
                    }
                    result.AppendLine();
                }
            }

            result.AppendLine("To retry a task, use: `{\"action\": \"retry\", \"task_id\": \"<task-id>\"}`");
            result.AppendLine("To retry all tasks in a project, use: `{\"action\": \"retry_all\", \"project_id\": \"<project-id>\"}`");

            return result.ToString();
        }

        private string RetryTask(Dictionary<string, object> arguments)
        {
            if (!arguments.TryGetValue("task_id", out var taskIdObj))
            {
                return "Error: Missing required 'task_id' parameter for 'retry' action";
            }

            var taskId = taskIdObj.ToString();
            if (string.IsNullOrEmpty(taskId))
            {
                return "Error: Invalid task_id";
            }

            // Find the task across all projects
            var allProjects = _projectService.GetAllProjects();

            foreach (var project in allProjects)
            {
                // Try Drakes first (in-memory)
                var drakes = _drakeFactory.GetDrakesForProject(project.Id);

                foreach (var (drake, drakeName) in drakes)
                {
                    var task = drake.GetAllTasks().FirstOrDefault(t => t.Id == taskId);

                    if (task != null)
                    {
                        if (task.Status != TaskStatus.Failed)
                        {
                            return $"⚠️ Task {taskId[..Math.Min(8, taskId.Length)]} is not in Failed status (current: {task.Status})";
                        }

                        // Reset task to Unassigned
                        drake.UpdateTask(task, TaskStatus.Unassigned);

                        // Clear error message by getting the kobold and resetting it
                        var kobold = drake.GetKoboldForTask(taskId);
                        if (kobold != null)
                        {
                            // Unsummon the failed kobold so a fresh one can be created
                            drake.UnsummonKobold(kobold.Id);
                        }

                        // Save tasks to file
                        drake.UpdateTasksFile();

                        return $"✅ Task {taskId[..Math.Min(8, taskId.Length)]} has been reset to Unassigned status and will be retried.\n" +
                               $"Project: {project.Name}\n" +
                               $"Task: {task.Task}";
                    }
                }

                // Fall back to file access if no Drakes found
                if (drakes.Count == 0)
                {
                    var result = RetryTaskFromFile(project, taskId);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return $"❌ Task {taskId[..Math.Min(8, taskId.Length)]} not found in any project";
        }

        /// <summary>
        /// Attempts to retry a task by loading it directly from task files on disk.
        /// Used as fallback when no Drakes are in memory (e.g., after server restart).
        /// </summary>
        private string? RetryTaskFromFile(Models.Projects.Project project, string taskId)
        {
            foreach (var (area, filePath) in project.Paths.TaskFiles)
            {
                var tracker = new TaskTracker();
                tracker.LoadFromFile(filePath);

                var task = tracker.GetTaskById(taskId);
                if (task == null) continue;

                if (task.Status != TaskStatus.Failed)
                {
                    return $"⚠️ Task {taskId[..Math.Min(8, taskId.Length)]} is not in Failed status (current: {task.Status})";
                }

                // Reset task to Unassigned and clear error state
                tracker.UpdateTask(task, TaskStatus.Unassigned);
                tracker.ClearError(task);

                // Save both JSON and markdown
                tracker.SaveToFile(filePath);

                return $"✅ Task {taskId[..Math.Min(8, taskId.Length)]} has been reset to Unassigned status and will be retried.\n" +
                       $"Project: {project.Name}\n" +
                       $"Task: {task.Task}\n" +
                       $"(Reset via file - task will be picked up when Drake starts)";
            }

            return null;
        }

        private string RetryAllProjectTasks(Dictionary<string, object> arguments)
        {
            if (!arguments.TryGetValue("project_id", out var projectIdObj))
            {
                return "Error: Missing required 'project_id' parameter for 'retry_all' action";
            }

            var projectId = projectIdObj.ToString();
            if (string.IsNullOrEmpty(projectId))
            {
                return "Error: Invalid project_id";
            }

            var project = _projectService.GetProject(projectId);
            if (project == null)
            {
                return $"❌ Project {projectId} not found";
            }

            var drakes = _drakeFactory.GetDrakesForProject(projectId);
            var retriedCount = 0;

            if (drakes.Count > 0)
            {
                // Use in-memory Drakes
                foreach (var (drake, drakeName) in drakes)
                {
                    var failedTasks = drake.GetAllTasks().Where(t => t.Status == TaskStatus.Failed).ToList();

                    foreach (var task in failedTasks)
                    {
                        drake.UpdateTask(task, TaskStatus.Unassigned);

                        var kobold = drake.GetKoboldForTask(task.Id);
                        if (kobold != null)
                        {
                            drake.UnsummonKobold(kobold.Id);
                        }

                        retriedCount++;
                    }

                    if (failedTasks.Count > 0)
                    {
                        drake.UpdateTasksFile();
                    }
                }
            }
            else
            {
                // Fall back to file access (e.g., after server restart)
                foreach (var (area, filePath) in project.Paths.TaskFiles)
                {
                    var tracker = new TaskTracker();
                    tracker.LoadFromFile(filePath);

                    var failedTasks = tracker.GetAllTasks().Where(t => t.Status == TaskStatus.Failed).ToList();
                    if (failedTasks.Count == 0) continue;

                    foreach (var task in failedTasks)
                    {
                        tracker.UpdateTask(task, TaskStatus.Unassigned);
                        tracker.ClearError(task);
                        retriedCount++;
                    }

                    tracker.SaveToFile(filePath);
                }
            }

            if (retriedCount == 0)
            {
                return $"✅ No failed tasks found in project {project.Name}";
            }

            var suffix = drakes.Count == 0
                ? "Tasks will be picked up when Drake starts."
                : "Tasks will be retried on the next Drake execution cycle.";

            return $"✅ Reset {retriedCount} failed task(s) to Unassigned status in project {project.Name}.\n{suffix}";
        }
    }
}
