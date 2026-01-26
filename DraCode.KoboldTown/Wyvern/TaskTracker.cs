using System.Text;

namespace DraCode.KoboldTown.Wyvern
{
    /// <summary>
    /// Manages task tracking and markdown report generation
    /// </summary>
    public class TaskTracker
    {
        private readonly List<TaskRecord> _tasks = new();
        private readonly object _lock = new();

        /// <summary>
        /// Add a new task to track
        /// </summary>
        public TaskRecord AddTask(string task)
        {
            lock (_lock)
            {
                var record = new TaskRecord
                {
                    Task = task,
                    Status = TaskStatus.Unassigned,
                    CreatedAt = DateTime.UtcNow
                };
                _tasks.Add(record);
                return record;
            }
        }

        /// <summary>
        /// Update the status of a task
        /// </summary>
        public void UpdateTask(TaskRecord task, TaskStatus status, string? assignedAgent = null)
        {
            lock (_lock)
            {
                task.Status = status;
                task.UpdatedAt = DateTime.UtcNow;
                if (assignedAgent != null)
                {
                    task.AssignedAgent = assignedAgent;
                }
            }
        }

        /// <summary>
        /// Mark a task as having an error
        /// </summary>
        public void SetError(TaskRecord task, string errorMessage)
        {
            lock (_lock)
            {
                task.ErrorMessage = errorMessage;
                task.UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Get all tracked tasks
        /// </summary>
        public List<TaskRecord> GetAllTasks()
        {
            lock (_lock)
            {
                return new List<TaskRecord>(_tasks);
            }
        }

        /// <summary>
        /// Get task by ID
        /// </summary>
        public TaskRecord? GetTaskById(string id)
        {
            lock (_lock)
            {
                return _tasks.FirstOrDefault(t => t.Id == id);
            }
        }

        /// <summary>
        /// Generate a markdown table from tracked tasks
        /// </summary>
        public string GenerateMarkdown(string? title = null)
        {
            lock (_lock)
            {
                var sb = new StringBuilder();

                if (!string.IsNullOrEmpty(title))
                {
                    sb.AppendLine($"# {title}");
                    sb.AppendLine();
                }

                sb.AppendLine("## Task Status Report");
                sb.AppendLine();
                sb.AppendLine($"*Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");
                sb.AppendLine();
                sb.AppendLine("| Task | Assigned Agent | Status |");
                sb.AppendLine("|------|----------------|--------|");

                foreach (var task in _tasks)
                {
                    var taskDisplay = task.Task.Length > 80 
                        ? task.Task.Substring(0, 77) + "..." 
                        : task.Task;
                    
                    // Escape pipe characters in task description
                    taskDisplay = taskDisplay.Replace("|", "\\|");

                    var agentDisplay = string.IsNullOrEmpty(task.AssignedAgent) 
                        ? "-" 
                        : task.AssignedAgent;

                    var statusDisplay = task.Status.ToString().ToLower();

                    // Add emoji for visual clarity
                    var statusWithEmoji = task.Status switch
                    {
                        TaskStatus.Unassigned => "⚪ unassigned",
                        TaskStatus.NotInitialized => "🔵 notinitialized",
                        TaskStatus.Working => "🟡 working",
                        TaskStatus.Done => "🟢 done",
                        _ => statusDisplay
                    };

                    if (!string.IsNullOrEmpty(task.ErrorMessage))
                    {
                        statusWithEmoji = "🔴 error";
                    }

                    sb.AppendLine($"| {taskDisplay} | {agentDisplay} | {statusWithEmoji} |");
                }

                sb.AppendLine();

                // Add summary statistics
                sb.AppendLine("## Summary");
                sb.AppendLine();
                sb.AppendLine($"- **Total Tasks**: {_tasks.Count}");
                sb.AppendLine($"- **Unassigned**: {_tasks.Count(t => t.Status == TaskStatus.Unassigned)}");
                sb.AppendLine($"- **Not Initialized**: {_tasks.Count(t => t.Status == TaskStatus.NotInitialized)}");
                sb.AppendLine($"- **Working**: {_tasks.Count(t => t.Status == TaskStatus.Working)}");
                sb.AppendLine($"- **Done**: {_tasks.Count(t => t.Status == TaskStatus.Done)}");
                
                var errorCount = _tasks.Count(t => !string.IsNullOrEmpty(t.ErrorMessage));
                if (errorCount > 0)
                {
                    sb.AppendLine($"- **Errors**: {errorCount}");
                }

                // Add error details if any
                var tasksWithErrors = _tasks.Where(t => !string.IsNullOrEmpty(t.ErrorMessage)).ToList();
                if (tasksWithErrors.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("## Errors");
                    sb.AppendLine();
                    foreach (var task in tasksWithErrors)
                    {
                        sb.AppendLine($"### Task: {task.Task.Substring(0, Math.Min(50, task.Task.Length))}...");
                        sb.AppendLine($"**Error**: {task.ErrorMessage}");
                        sb.AppendLine();
                    }
                }

                return sb.ToString();
            }
        }

        /// <summary>
        /// Save the markdown report to a file
        /// </summary>
        public void SaveToFile(string filePath, string? title = null)
        {
            var markdown = GenerateMarkdown(title);
            File.WriteAllText(filePath, markdown);
        }

        /// <summary>
        /// Clear all tracked tasks
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _tasks.Clear();
            }
        }
    }
}
