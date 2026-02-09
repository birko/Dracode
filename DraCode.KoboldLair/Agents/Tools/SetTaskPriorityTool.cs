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
                    var drakes = _drakeFactory.GetDrakesForProject(project.Id);

                    foreach (var (drake, drakeName) in drakes)
                    {
                        var task = drake.GetAllTasks().FirstOrDefault(t => t.Id == taskId);

                        if (task != null)
                        {
                            // Check if task is already complete
                            if (task.Status == TaskStatus.Done)
                            {
                                return $"⚠️ Task {taskId[..Math.Min(8, taskId.Length)]} is already completed. Cannot change priority.";
                            }

                            var oldPriority = task.Priority;
                            task.Priority = priority.Value;

                            // Save tasks to file to persist the change
                            drake.UpdateTasksFile();

                            var priorityIcon = priority.Value switch
                            {
                                TaskPriority.Critical => "🔴",
                                TaskPriority.High => "🟠",
                                TaskPriority.Normal => "🟡",
                                TaskPriority.Low => "🟢",
                                _ => "⚪"
                            };

                            return $"✅ Task priority updated successfully\n" +
                                   $"Project: {project.Name}\n" +
                                   $"Task ID: {taskId[..Math.Min(8, taskId.Length)]}\n" +
                                   $"Task: {task.Task}\n" +
                                   $"Priority: {oldPriority} → {priorityIcon} {priority.Value}\n" +
                                   $"Status: {task.Status}\n\n" +
                                   $"The task will be scheduled according to its new priority on the next execution cycle.";
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
    }
}
