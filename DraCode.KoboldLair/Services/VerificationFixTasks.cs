using System.Text.RegularExpressions;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using Microsoft.Extensions.Logging;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// The fix tasks a failed project verification adds to the <c>verification-fixes</c> area (TASK-178). Written through
    /// <see cref="TaskTracker"/>, as Wyvern writes every other area, so Drake loads them from the JSON beside the markdown.
    /// </summary>
    public static class VerificationFixTasks
    {
        public const string AreaName = "verification-fixes";
        public const string FileName = "verification-fixes-tasks.md";
        private const string Title = "KoboldLair verification-fixes Tasks";
        private const int MaxErrorLength = 200;

        /// <summary>How each fix task's text starts; a task with this start that is not Done is the check type's open fix.</summary>
        public static string DescriptionPrefix(string checkType) => $"Fix {checkType} failure:";

        /// <summary>
        /// Adds one fix task per failed check to the area file at <paramref name="taskFilePath"/>, skipping a check type
        /// whose fix task is still open, and saves the file only when something was added. Returns the added tasks.
        /// </summary>
        public static async Task<IReadOnlyList<TaskRecord>> AddAsync(
            string taskFilePath, IEnumerable<VerificationCheck> failedChecks, ILogger? logger = null)
        {
            var tracker = new TaskTracker();
            await tracker.LoadFromFileAsync(taskFilePath);

            var added = new List<TaskRecord>();
            foreach (var check in failedChecks)
            {
                var prefix = DescriptionPrefix(check.CheckType);
                var open = tracker.GetAllTasks().FirstOrDefault(t =>
                    t.Status != TaskStatus.Done && t.Task.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                if (open != null)
                {
                    logger?.LogDebug("Fix task {TaskId} for {CheckType} is still open; not adding another", open.Id, check.CheckType);
                    continue;
                }

                var error = Regex.Replace(check.Output ?? "", @"\s+", " ").Trim();
                if (error.Length > MaxErrorLength)
                    error = error[..MaxErrorLength] + "...";

                var task = tracker.AddTask($"{prefix} {check.Command}. Error: {error}", PriorityOf(check.Priority));
                added.Add(task);
                logger?.LogInformation("Created fix task {TaskId} for {CheckType} failure", task.Id, check.CheckType);
            }

            if (added.Count > 0)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(taskFilePath))!);
                await tracker.SaveToFileAsync(taskFilePath, Title);
            }
            return added;
        }

        private static TaskPriority PriorityOf(VerificationCheckPriority priority) => priority switch
        {
            VerificationCheckPriority.Critical => TaskPriority.Critical,
            VerificationCheckPriority.High => TaskPriority.High,
            VerificationCheckPriority.Medium => TaskPriority.Normal,
            _ => TaskPriority.Low
        };
    }
}
