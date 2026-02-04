using System.Text;
using System.Text.RegularExpressions;

namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// Manages task tracking and markdown report generation
    /// </summary>
    public class TaskTracker
    {
        private readonly List<TaskRecord> _tasks = new();
        private readonly object _lock = new();

        // Regex to match markdown table rows: | Task | Agent | Status |
        private static readonly Regex TableRowRegex = new(
            @"^\|\s*(.+?)\s*\|\s*(.+?)\s*\|\s*(.+?)\s*\|$",
            RegexOptions.Compiled);

        // Regex to match status with emoji: "🟡 working" or just "working"
        private static readonly Regex StatusRegex = new(
            @"^(?:[⚪🔵🟡🟢🔴]\s*)?(\w+)$",
            RegexOptions.Compiled);

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
                    // Remove newlines and normalize whitespace for table display
                    var taskNormalized = Regex.Replace(task.Task, @"\s+", " ").Trim();

                    var taskDisplay = taskNormalized.Length > 80
                        ? taskNormalized.Substring(0, 77) + "..."
                        : taskNormalized;

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

        /// <summary>
        /// Loads tasks from a markdown file, restoring task state.
        /// Parses the markdown table format generated by GenerateMarkdown().
        /// Note: Task IDs are regenerated as they are not stored in the markdown.
        /// </summary>
        /// <param name="filePath">Path to the markdown file</param>
        /// <returns>Number of tasks loaded</returns>
        public int LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return 0;
            }

            var content = File.ReadAllText(filePath);
            return LoadFromMarkdown(content);
        }

        /// <summary>
        /// Loads tasks from markdown content, restoring task state.
        /// Parses the markdown table format generated by GenerateMarkdown().
        /// </summary>
        /// <param name="markdown">Markdown content to parse</param>
        /// <returns>Number of tasks loaded</returns>
        public int LoadFromMarkdown(string markdown)
        {
            lock (_lock)
            {
                var lines = markdown.Split('\n');
                var tasksLoaded = 0;
                var inTable = false;
                var headerPassed = false;

                foreach (var rawLine in lines)
                {
                    var line = rawLine.Trim();

                    // Detect table start (header row)
                    if (line.StartsWith("| Task |") || line.StartsWith("|Task|"))
                    {
                        inTable = true;
                        headerPassed = false;
                        continue;
                    }

                    // Skip separator row (|------|...)
                    if (inTable && line.StartsWith("|") && line.Contains("---"))
                    {
                        headerPassed = true;
                        continue;
                    }

                    // End of table detection
                    if (inTable && headerPassed && !line.StartsWith("|"))
                    {
                        inTable = false;
                        continue;
                    }

                    // Parse data rows
                    if (inTable && headerPassed && line.StartsWith("|"))
                    {
                        var task = ParseTableRow(line);
                        if (task != null)
                        {
                            _tasks.Add(task);
                            tasksLoaded++;
                        }
                    }
                }

                return tasksLoaded;
            }
        }

        /// <summary>
        /// Parses a single markdown table row into a TaskRecord
        /// </summary>
        private TaskRecord? ParseTableRow(string line)
        {
            var match = TableRowRegex.Match(line);
            if (!match.Success)
            {
                return null;
            }

            var taskDescription = match.Groups[1].Value.Trim();
            var agentString = match.Groups[2].Value.Trim();
            var statusString = match.Groups[3].Value.Trim();

            // Skip if task is empty or this looks like a header
            if (string.IsNullOrWhiteSpace(taskDescription) ||
                taskDescription.Equals("Task", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Unescape pipe characters
            taskDescription = taskDescription.Replace("\\|", "|");

            // Parse agent (handle "-" as empty)
            var assignedAgent = agentString == "-" ? string.Empty : agentString;

            // Parse status
            var status = ParseStatus(statusString);

            // Check for error status
            string? errorMessage = null;
            if (statusString.Contains("error", StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Task had error (details lost during reload)";
            }

            return new TaskRecord
            {
                Task = taskDescription,
                AssignedAgent = assignedAgent,
                Status = status,
                ErrorMessage = errorMessage,
                CreatedAt = DateTime.UtcNow // Original creation time is not preserved
            };
        }

        /// <summary>
        /// Parses a status string (with optional emoji) into TaskStatus
        /// </summary>
        private static TaskStatus ParseStatus(string statusString)
        {
            // Extract the status word from patterns like "🟡 working" or "working"
            var match = StatusRegex.Match(statusString);
            var statusWord = match.Success ? match.Groups[1].Value : statusString;

            return statusWord.ToLowerInvariant() switch
            {
                "unassigned" => TaskStatus.Unassigned,
                "notinitialized" => TaskStatus.NotInitialized,
                "working" => TaskStatus.Working,
                "done" => TaskStatus.Done,
                "error" => TaskStatus.Done, // Errors are considered "done" but with error message
                _ => TaskStatus.Unassigned // Default for unknown status
            };
        }

        /// <summary>
        /// Gets the count of tasks by status
        /// </summary>
        public Dictionary<TaskStatus, int> GetStatusCounts()
        {
            lock (_lock)
            {
                return _tasks
                    .GroupBy(t => t.Status)
                    .ToDictionary(g => g.Key, g => g.Count());
            }
        }
    }
}
