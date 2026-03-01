using DraCode.Agent.Tools;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for manually setting task priority. Allows users to override
    /// Wyvern's automatic priority assignment for specific tasks.
    /// </summary>
    public class SetTaskPriorityTool : Tool
    {
        private readonly DrakeFactory _drakeFactory;
        private readonly ProjectService _projectService;

        public SetTaskPriorityTool(DrakeFactory drakeFactory, ProjectService projectService)
        {
            _drakeFactory = drakeFactory;
            _projectService = projectService;
        }

        public override string Name => "set_task_priority";

        public override string Description => @"Sets the priority of a task to control execution order.
Higher priority tasks are executed before lower priority tasks (when dependencies allow).

Priority levels:
- 'critical' - Blocking tasks, infrastructure, core dependencies
- 'high' - Important core features
- 'normal' - Standard features (default)
- 'low' - Nice-to-have features, polish, documentation

Examples:
- Set task to critical: {""task_id"": ""abc123"", ""priority"": ""critical""}
- Set task to low: {""task_id"": ""abc123"", ""priority"": ""low""}

Note: Dependencies always take precedence - a task cannot run before its dependencies
complete, regardless of priority.";

        public override object InputSchema => new
        {
            type = "object",
            properties = new
            {
                task_id = new
                {
                    type = "string",
                    description = "ID of the task to update"
                },
                priority = new
                {
                    type = "string",
                    description = "New priority level for the task",
                    @enum = new[] { "critical", "high", "normal", "low" }
                }
            },
            required = new[] { "task_id", "priority" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> arguments)
        {
            try
            {
                if (!arguments.TryGetValue("task_id", out var taskIdObj))
                {
                    return "Error: Missing required 'task_id' parameter";
                }

                if (!arguments.TryGetValue("priority", out var priorityObj))
                {
                    return "Error: Missing required 'priority' parameter";
                }

                var taskId = taskIdObj.ToString();
                var priorityStr = priorityObj.ToString()?.ToLowerInvariant();

                if (string.IsNullOrEmpty(taskId))
                {
                    return "Error: Invalid task_id";
                }

                if (string.IsNullOrEmpty(priorityStr))
                {
                    return "Error: Invalid priority";
                }

                // Parse priority
                var priority = priorityStr switch
                {
                    "critical" => TaskPriority.Critical,
                    "high" => TaskPriority.High,
                    "normal" => TaskPriority.Normal,
                    "low" => TaskPriority.Low,
                    _ => (TaskPriority?)null
                };

                if (priority == null)
                {
                    return $"Error: Invalid priority '{priorityStr}'. Must be one of: critical, high, normal, low";
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
                            if (task.Status == TaskStatus.Done)
                            {
                                return $"⚠️ Task {taskId[..Math.Min(8, taskId.Length)]} is already completed. Cannot change priority.";
                            }

                            var oldPriority = task.Priority;
                            task.Priority = priority.Value;

                            drake.UpdateTasksFile();

                            return FormatSuccess(project.Name, taskId, task.Task, oldPriority, priority.Value, task.Status);
                        }
                    }

                    // Fall back to file access if no Drakes found (e.g., after server restart)
                    if (drakes.Count == 0)
                    {
                        var result = SetPriorityFromFile(project, taskId, priority.Value);
                        if (result != null)
                        {
                            return result;
                        }
                    }
                }

                return $"❌ Task {taskId[..Math.Min(8, taskId.Length)]} not found in any project";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Sets task priority by loading directly from task files on disk.
        /// Used as fallback when no Drakes are in memory (e.g., after server restart).
        /// </summary>
        private string? SetPriorityFromFile(Models.Projects.Project project, string taskId, TaskPriority priority)
        {
            foreach (var (area, filePath) in project.Paths.TaskFiles)
            {
                var tracker = new TaskTracker();
                tracker.LoadFromFile(filePath);

                var task = tracker.GetTaskById(taskId);
                if (task == null) continue;

                if (task.Status == TaskStatus.Done)
                {
                    return $"⚠️ Task {taskId[..Math.Min(8, taskId.Length)]} is already completed. Cannot change priority.";
                }

                var oldPriority = task.Priority;
                task.Priority = priority;

                tracker.SaveToFile(filePath);

                return FormatSuccess(project.Name, taskId, task.Task, oldPriority, priority, task.Status,
                    "(Updated via file - change will take effect when Drake starts)");
            }

            return null;
        }

        private static string FormatSuccess(string projectName, string taskId, string taskDescription,
            TaskPriority oldPriority, TaskPriority newPriority, TaskStatus status, string? suffix = null)
        {
            var priorityIcon = newPriority switch
            {
                TaskPriority.Critical => "🔴",
                TaskPriority.High => "🟠",
                TaskPriority.Normal => "🟡",
                TaskPriority.Low => "🟢",
                _ => "⚪"
            };

            var result = $"✅ Task priority updated successfully\n" +
                         $"Project: {projectName}\n" +
                         $"Task ID: {taskId[..Math.Min(8, taskId.Length)]}\n" +
                         $"Task: {taskDescription}\n" +
                         $"Priority: {oldPriority} → {priorityIcon} {newPriority}\n" +
                         $"Status: {status}\n\n" +
                         (suffix ?? "The task will be scheduled according to its new priority on the next execution cycle.");

            return result;
        }
    }
}
