using Birko.AI.Tools;
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

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> arguments, CancellationToken cancellationToken = default)
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

                // Through DrakeFactory, the same way the REST priority call does it (TASK-154)
                foreach (var project in _projectService.GetAllProjects())
                {
                    var change = await _drakeFactory.SetTaskPriorityAsync(project, taskId, priority.Value);
                    switch (change.Outcome)
                    {
                        case TaskPriorityOutcome.Set:
                            return FormatSuccess(project.Name, taskId, change.Task!.Task, change.Previous!.Value, priority.Value, change.Task.Status);
                        case TaskPriorityOutcome.AlreadyDone:
                            return $"⚠️ Task {taskId[..Math.Min(8, taskId.Length)]} is already completed. Cannot change priority.";
                    }
                }

                return $"❌ Task {taskId[..Math.Min(8, taskId.Length)]} not found in any project";
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }

        private static string FormatSuccess(string projectName, string taskId, string taskDescription,
            TaskPriority oldPriority, TaskPriority newPriority, TaskStatus status)
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
                         "The task will be scheduled according to its new priority on the next execution cycle.";

            return result;
        }
    }
}
