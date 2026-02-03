using System.Text;
using System.Text.Json;
using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Service for managing Kobold implementation plans.
    /// Handles persistence of plans to both JSON (for machine reading) and Markdown (for human reading).
    /// Plans are stored in {ProjectsPath}/{project-name}/kobold-plans/
    /// </summary>
    public class KoboldPlanService
    {
        private readonly string _projectsPath;
        private readonly ILogger<KoboldPlanService>? _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public KoboldPlanService(string projectsPath, ILogger<KoboldPlanService>? logger = null)
        {
            _projectsPath = projectsPath;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Gets the plans directory for a project
        /// </summary>
        private string GetPlansDirectory(string projectId)
        {
            return Path.Combine(_projectsPath, projectId, "kobold-plans");
        }

        /// <summary>
        /// Gets the JSON file path for a plan
        /// </summary>
        private string GetPlanJsonPath(string projectId, string taskId)
        {
            return Path.Combine(GetPlansDirectory(projectId), $"{taskId}-plan.json");
        }

        /// <summary>
        /// Gets the Markdown file path for a plan
        /// </summary>
        private string GetPlanMarkdownPath(string projectId, string taskId)
        {
            return Path.Combine(GetPlansDirectory(projectId), $"{taskId}-plan.md");
        }

        /// <summary>
        /// Saves a plan to disk (both JSON and Markdown)
        /// </summary>
        public async Task SavePlanAsync(KoboldImplementationPlan plan)
        {
            if (string.IsNullOrEmpty(plan.ProjectId) || string.IsNullOrEmpty(plan.TaskId))
            {
                throw new ArgumentException("Plan must have ProjectId and TaskId set");
            }

            var plansDir = GetPlansDirectory(plan.ProjectId);
            Directory.CreateDirectory(plansDir);

            plan.UpdatedAt = DateTime.UtcNow;

            // Save JSON
            var jsonPath = GetPlanJsonPath(plan.ProjectId, plan.TaskId);
            var json = JsonSerializer.Serialize(plan, _jsonOptions);
            await File.WriteAllTextAsync(jsonPath, json);

            // Save Markdown
            var mdPath = GetPlanMarkdownPath(plan.ProjectId, plan.TaskId);
            var markdown = GeneratePlanMarkdown(plan);
            await File.WriteAllTextAsync(mdPath, markdown);

            _logger?.LogDebug("Saved plan for task {TaskId} in project {ProjectId}",
                plan.TaskId[..Math.Min(8, plan.TaskId.Length)], plan.ProjectId);
        }

        /// <summary>
        /// Loads a plan from disk
        /// </summary>
        public async Task<KoboldImplementationPlan?> LoadPlanAsync(string projectId, string taskId)
        {
            var jsonPath = GetPlanJsonPath(projectId, taskId);

            if (!File.Exists(jsonPath))
            {
                return null;
            }

            try
            {
                var json = await File.ReadAllTextAsync(jsonPath);
                var plan = JsonSerializer.Deserialize<KoboldImplementationPlan>(json, _jsonOptions);

                if (plan != null)
                {
                    _logger?.LogDebug("Loaded plan for task {TaskId} from {Path}",
                        taskId[..Math.Min(8, taskId.Length)], jsonPath);
                }

                return plan;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load plan from {Path}", jsonPath);
                return null;
            }
        }

        /// <summary>
        /// Checks if a plan exists for the given project and task
        /// </summary>
        public Task<bool> PlanExistsAsync(string projectId, string taskId)
        {
            var jsonPath = GetPlanJsonPath(projectId, taskId);
            return Task.FromResult(File.Exists(jsonPath));
        }

        /// <summary>
        /// Deletes a plan from disk
        /// </summary>
        public Task DeletePlanAsync(string projectId, string taskId)
        {
            var jsonPath = GetPlanJsonPath(projectId, taskId);
            var mdPath = GetPlanMarkdownPath(projectId, taskId);

            if (File.Exists(jsonPath))
            {
                File.Delete(jsonPath);
            }

            if (File.Exists(mdPath))
            {
                File.Delete(mdPath);
            }

            _logger?.LogDebug("Deleted plan for task {TaskId} in project {ProjectId}",
                taskId[..Math.Min(8, taskId.Length)], projectId);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets all plans for a project
        /// </summary>
        public async Task<List<KoboldImplementationPlan>> GetPlansForProjectAsync(string projectId)
        {
            var plansDir = GetPlansDirectory(projectId);
            var plans = new List<KoboldImplementationPlan>();

            if (!Directory.Exists(plansDir))
            {
                return plans;
            }

            foreach (var jsonFile in Directory.GetFiles(plansDir, "*-plan.json"))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(jsonFile);
                    var plan = JsonSerializer.Deserialize<KoboldImplementationPlan>(json, _jsonOptions);
                    if (plan != null)
                    {
                        plans.Add(plan);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load plan from {Path}", jsonFile);
                }
            }

            return plans.OrderByDescending(p => p.UpdatedAt).ToList();
        }

        /// <summary>
        /// Generates a human-readable Markdown representation of the plan
        /// </summary>
        public string GeneratePlanMarkdown(KoboldImplementationPlan plan)
        {
            var sb = new StringBuilder();

            // Header
            var taskTitle = plan.TaskDescription.Length > 60
                ? plan.TaskDescription[..60] + "..."
                : plan.TaskDescription;
            sb.AppendLine($"# Implementation Plan: {taskTitle}");
            sb.AppendLine();

            // Metadata
            sb.AppendLine($"**Task ID:** `{plan.TaskId}`");
            sb.AppendLine($"**Project:** {plan.ProjectId}");
            sb.AppendLine($"**Created:** {plan.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"**Updated:** {plan.UpdatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine($"**Status:** {GetStatusEmoji(plan.Status)} {plan.Status}");
            sb.AppendLine($"**Progress:** {plan.CompletedStepsCount}/{plan.Steps.Count} steps ({plan.ProgressPercentage}%)");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(plan.ErrorMessage))
            {
                sb.AppendLine($"> **Error:** {plan.ErrorMessage}");
                sb.AppendLine();
            }

            // Task description
            sb.AppendLine("## Task Description");
            sb.AppendLine();
            sb.AppendLine(plan.TaskDescription);
            sb.AppendLine();

            // Steps summary table
            sb.AppendLine("## Steps Overview");
            sb.AppendLine();
            sb.AppendLine("| # | Step | Status | Files |");
            sb.AppendLine("|---|------|--------|-------|");

            foreach (var step in plan.Steps)
            {
                var statusIcon = GetStepStatusIcon(step.Status);
                var files = new List<string>();
                files.AddRange(step.FilesToCreate.Select(f => $"+{f}"));
                files.AddRange(step.FilesToModify.Select(f => $"~{f}"));
                var filesStr = files.Count > 0 ? string.Join(", ", files.Take(3)) : "-";
                if (files.Count > 3) filesStr += $" (+{files.Count - 3})";

                sb.AppendLine($"| {step.Index} | {step.Title} | {statusIcon} {step.Status} | {filesStr} |");
            }

            sb.AppendLine();

            // Step details
            sb.AppendLine("## Step Details");
            sb.AppendLine();

            foreach (var step in plan.Steps)
            {
                var statusIcon = GetStepStatusIcon(step.Status);
                sb.AppendLine($"### Step {step.Index}: {step.Title}");
                sb.AppendLine();
                sb.AppendLine($"**Status:** {statusIcon} {step.Status}");

                if (step.StartedAt.HasValue)
                {
                    sb.AppendLine($"**Started:** {step.StartedAt:HH:mm:ss}");
                }

                if (step.CompletedAt.HasValue)
                {
                    sb.AppendLine($"**Completed:** {step.CompletedAt:HH:mm:ss}");
                }

                sb.AppendLine();

                if (step.FilesToCreate.Count > 0)
                {
                    sb.AppendLine("**Files to create:**");
                    foreach (var file in step.FilesToCreate)
                    {
                        sb.AppendLine($"- `{file}`");
                    }
                    sb.AppendLine();
                }

                if (step.FilesToModify.Count > 0)
                {
                    sb.AppendLine("**Files to modify:**");
                    foreach (var file in step.FilesToModify)
                    {
                        sb.AppendLine($"- `{file}`");
                    }
                    sb.AppendLine();
                }

                sb.AppendLine("**Description:**");
                sb.AppendLine();
                sb.AppendLine(step.Description);
                sb.AppendLine();

                if (!string.IsNullOrEmpty(step.Output))
                {
                    sb.AppendLine("**Output:**");
                    sb.AppendLine();
                    sb.AppendLine("```");
                    sb.AppendLine(step.Output);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Execution log
            if (plan.ExecutionLog.Count > 0)
            {
                sb.AppendLine("## Execution Log");
                sb.AppendLine();
                foreach (var entry in plan.ExecutionLog.TakeLast(20))
                {
                    sb.AppendLine($"- [{entry.Timestamp:HH:mm:ss}] {entry.Message}");
                }
                if (plan.ExecutionLog.Count > 20)
                {
                    sb.AppendLine($"- ... ({plan.ExecutionLog.Count - 20} earlier entries omitted)");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string GetStatusEmoji(PlanStatus status) => status switch
        {
            PlanStatus.Planning => "🔄",
            PlanStatus.Ready => "✅",
            PlanStatus.InProgress => "⚡",
            PlanStatus.Completed => "🎉",
            PlanStatus.Failed => "❌",
            _ => "❓"
        };

        private static string GetStepStatusIcon(StepStatus status) => status switch
        {
            StepStatus.Pending => "⏳",
            StepStatus.InProgress => "▶️",
            StepStatus.Completed => "✅",
            StepStatus.Skipped => "⏭️",
            StepStatus.Failed => "❌",
            _ => "❓"
        };
    }
}
