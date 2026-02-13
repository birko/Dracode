using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DraCode.Agent;
using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Service for managing Kobold implementation plans.
    /// Handles persistence of plans to both JSON (for machine reading) and Markdown (for human reading).
    /// Plans are stored in {ProjectOutputPath}/kobold-plans/ with human-readable filenames.
    /// </summary>
    public class KoboldPlanService
    {
        private readonly string _projectsPath;
        private readonly ProjectRepository? _projectRepository;
        private readonly ILogger<KoboldPlanService>? _logger;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly SemaphoreSlim _indexLock = new SemaphoreSlim(1, 1);

        private const string IndexFileName = "plan-index.json";
        private const int MaxFilenameDescriptionLength = 40;

        public KoboldPlanService(string projectsPath, ILogger<KoboldPlanService>? logger = null, ProjectRepository? projectRepository = null)
        {
            _projectsPath = projectsPath;
            _projectRepository = projectRepository;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };
        }

        /// <summary>
        /// Gets the project output path by resolving project ID through the repository.
        /// Throws an exception if the repository is not available or the project cannot be found.
        /// Ensures the returned path is absolute by combining relative paths with _projectsPath.
        /// </summary>
        private string GetProjectOutputPath(string projectId)
        {
            if (_projectRepository == null)
            {
                throw new InvalidOperationException(
                    $"Cannot resolve project output path for project '{projectId}': ProjectRepository is not configured. " +
                    "KoboldPlanService requires a ProjectRepository to function correctly.");
            }

            var project = _projectRepository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException(
                    $"Cannot resolve project output path: Project '{projectId}' not found in repository. " +
                    "Ensure the project is registered before creating plans.");
            }

            if (string.IsNullOrEmpty(project.Paths.Output))
            {
                throw new InvalidOperationException(
                    $"Cannot resolve project output path: Project '{projectId}' (Name: '{project.Name}') has no output path configured. " +
                    "Ensure the project is properly initialized.");
            }

            var outputPath = project.Paths.Output;
            // Handle relative paths by combining with projectsPath
            if (!Path.IsPathRooted(outputPath))
            {
                outputPath = Path.Combine(_projectsPath, outputPath);
            }
            return Path.GetFullPath(outputPath);
        }

        /// <summary>
        /// Gets the plans directory for a project
        /// </summary>
        private string GetPlansDirectory(string projectId)
        {
            var outputPath = GetProjectOutputPath(projectId);
            return Path.Combine(outputPath, "kobold-plans");
        }

        /// <summary>
        /// Gets the plan index file path for a project
        /// </summary>
        private string GetPlanIndexPath(string projectId)
        {
            return Path.Combine(GetPlansDirectory(projectId), IndexFileName);
        }

        /// <summary>
        /// Generates a human-readable filename from a task description.
        /// Format: {sanitized-description}-{4char-hash}
        /// Example: "[frontend-1] Create user auth" -> "frontend-1-create-user-auth-a7f3"
        /// </summary>
        public static string GeneratePlanFilename(string taskDescription, string taskId)
        {
            // Extract meaningful words from task description
            var cleaned = taskDescription;

            // Remove markdown formatting
            cleaned = Regex.Replace(cleaned, @"\*\*|\*|__|_|`", "");

            // Extract content from brackets like [frontend-1]
            var bracketMatch = Regex.Match(cleaned, @"^\[([^\]]+)\]");
            var prefix = bracketMatch.Success ? bracketMatch.Groups[1].Value : "";
            if (bracketMatch.Success)
            {
                cleaned = cleaned[(bracketMatch.Index + bracketMatch.Length)..].Trim();
            }

            // Remove common prefixes like "Task:", "Implement:", etc.
            cleaned = Regex.Replace(cleaned, @"^(Task|Implement|Create|Add|Fix|Update|Build|Setup|Configure):\s*", "", RegexOptions.IgnoreCase);

            // Keep only alphanumeric, spaces, and hyphens
            cleaned = Regex.Replace(cleaned, @"[^a-zA-Z0-9\s-]", " ");

            // Split into words
            var words = cleaned.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries);

            // Build filename with max length
            var result = new StringBuilder();
            if (!string.IsNullOrEmpty(prefix))
            {
                result.Append(SanitizeForFilename(prefix));
            }

            var currentLength = result.Length;
            foreach (var word in words)
            {
                var sanitized = SanitizeForFilename(word);
                if (string.IsNullOrEmpty(sanitized)) continue;

                // Check if adding this word would exceed max length
                var separator = result.Length > 0 ? "-" : "";
                if (currentLength + separator.Length + sanitized.Length > MaxFilenameDescriptionLength)
                {
                    break;
                }

                result.Append(separator);
                result.Append(sanitized);
                currentLength = result.Length;
            }

            // Generate 4-character hash from taskId for uniqueness
            var hash = GenerateShortHash(taskId);

            // Combine: description-hash
            var filename = result.Length > 0
                ? $"{result}-{hash}"
                : hash; // Fallback to just hash if description is empty

            return filename.ToLowerInvariant();
        }

        /// <summary>
        /// Sanitizes a string for use in a filename
        /// </summary>
        private static string SanitizeForFilename(string input)
        {
            // Remove invalid filename characters and convert to lowercase
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = string.Join("", input.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
            
            // Replace spaces and multiple hyphens with single hyphen
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\s+", "-");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"-+", "-");
            
            return sanitized.Trim('-').ToLowerInvariant();
        }

        /// <summary>
        /// Generates a 4-character hash from a string (typically taskId)
        /// </summary>
        private static string GenerateShortHash(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hash = MD5.HashData(bytes);
            return Convert.ToHexString(hash)[..4].ToLowerInvariant();
        }

        /// <summary>
        /// Loads the plan index for a project with file locking to prevent concurrent access issues
        /// </summary>
        private async Task<Dictionary<string, string>> LoadPlanIndexAsync(string projectId)
        {
            await _indexLock.WaitAsync();
            try
            {
                var indexPath = GetPlanIndexPath(projectId);

                if (!File.Exists(indexPath))
                {
                    return new Dictionary<string, string>();
                }

                try
                {
                    var json = await File.ReadAllTextAsync(indexPath);
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(json, _jsonOptions)
                        ?? new Dictionary<string, string>();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load plan index from {Path}", indexPath);
                    return new Dictionary<string, string>();
                }
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// Saves the plan index for a project with file locking to prevent concurrent access issues
        /// </summary>
        private async Task SavePlanIndexAsync(string projectId, Dictionary<string, string> index)
        {
            await _indexLock.WaitAsync();
            try
            {
                var indexPath = GetPlanIndexPath(projectId);
                var json = JsonSerializer.Serialize(index, _jsonOptions);
                await File.WriteAllTextAsync(indexPath, json);
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// Gets the JSON file path for a plan using human-readable filename
        /// </summary>
        private string GetPlanJsonPath(string projectId, string planFilename)
        {
            return Path.Combine(GetPlansDirectory(projectId), $"{planFilename}-plan.json");
        }

        /// <summary>
        /// Gets the Markdown file path for a plan using human-readable filename
        /// </summary>
        private string GetPlanMarkdownPath(string projectId, string planFilename)
        {
            return Path.Combine(GetPlansDirectory(projectId), $"{planFilename}-plan.md");
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

            // Generate human-readable filename if not already set
            if (string.IsNullOrEmpty(plan.PlanFilename))
            {
                plan.PlanFilename = GeneratePlanFilename(plan.TaskDescription, plan.TaskId);
            }

            // Save JSON with human-readable filename
            var jsonPath = GetPlanJsonPath(plan.ProjectId, plan.PlanFilename);
            var json = JsonSerializer.Serialize(plan, _jsonOptions);
            await File.WriteAllTextAsync(jsonPath, json);

            // Save Markdown with human-readable filename
            var mdPath = GetPlanMarkdownPath(plan.ProjectId, plan.PlanFilename);
            var markdown = GeneratePlanMarkdown(plan);
            await File.WriteAllTextAsync(mdPath, markdown);

            // Update index
            var index = await LoadPlanIndexAsync(plan.ProjectId);
            index[plan.TaskId] = plan.PlanFilename;
            await SavePlanIndexAsync(plan.ProjectId, index);

            _logger?.LogDebug("Saved plan '{PlanFilename}' for task {TaskId} in project {ProjectId}",
                plan.PlanFilename, plan.TaskId[..Math.Min(8, plan.TaskId.Length)], plan.ProjectId);
        }

        /// <summary>
        /// Loads a plan from disk
        /// </summary>
        public async Task<KoboldImplementationPlan?> LoadPlanAsync(string projectId, string taskId)
        {
            // Check the index for human-readable filename
            var index = await LoadPlanIndexAsync(projectId);
            
            if (!index.TryGetValue(taskId, out var planFilename))
            {
                return null;
            }

            var jsonPath = GetPlanJsonPath(projectId, planFilename);
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
        public async Task<bool> PlanExistsAsync(string projectId, string taskId)
        {
            var index = await LoadPlanIndexAsync(projectId);
            if (index.TryGetValue(taskId, out var planFilename))
            {
                var jsonPath = GetPlanJsonPath(projectId, planFilename);
                return File.Exists(jsonPath);
            }
            
            return false;
        }

        /// <summary>
        /// Deletes a plan from disk
        /// </summary>
        public async Task DeletePlanAsync(string projectId, string taskId)
        {
            // Check index for human-readable filename
            var index = await LoadPlanIndexAsync(projectId);

            if (index.TryGetValue(taskId, out var planFilename))
            {
                var jsonPath = GetPlanJsonPath(projectId, planFilename);
                var mdPath = GetPlanMarkdownPath(projectId, planFilename);

                if (File.Exists(jsonPath)) await Task.Run(() => File.Delete(jsonPath));
                if (File.Exists(mdPath)) await Task.Run(() => File.Delete(mdPath));

                // Remove from index
                index.Remove(taskId);
                await SavePlanIndexAsync(projectId, index);
            }

            _logger?.LogDebug("Deleted plan for task {TaskId} in project {ProjectId}",
                taskId[..Math.Min(8, taskId.Length)], projectId);
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
            if (!string.IsNullOrEmpty(plan.PlanFilename))
            {
                sb.AppendLine($"**Plan File:** `{plan.PlanFilename}`");
            }
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

        /// <summary>
        /// Gets the conversation checkpoint file path for a plan
        /// </summary>
        private string GetCheckpointPath(string projectId, string planFilename)
        {
            return Path.Combine(GetPlansDirectory(projectId), $"{planFilename}-context.json");
        }

        /// <summary>
        /// Saves a conversation checkpoint for a plan. Trims to last 50 messages
        /// and serializes all content to JsonElement for safe round-tripping.
        /// Stored separately from plan JSON to keep plan files lean.
        /// </summary>
        public async Task SaveConversationCheckpointAsync(
            KoboldImplementationPlan plan,
            List<Message> conversation)
        {
            if (string.IsNullOrEmpty(plan.ProjectId) || string.IsNullOrEmpty(plan.TaskId) || string.IsNullOrEmpty(plan.PlanFilename))
            {
                _logger?.LogWarning("Cannot save conversation checkpoint: plan missing ProjectId, TaskId, or PlanFilename");
                return;
            }

            try
            {
                var plansDir = GetPlansDirectory(plan.ProjectId);
                Directory.CreateDirectory(plansDir);

                // Trim to last 50 messages to keep checkpoint manageable
                const int maxMessages = 50;
                var trimmedConversation = conversation.Count > maxMessages
                    ? conversation.Skip(conversation.Count - maxMessages).ToList()
                    : conversation;

                var checkpoint = new ConversationCheckpoint
                {
                    TaskId = plan.TaskId,
                    ProjectId = plan.ProjectId,
                    StepIndex = plan.CurrentStepIndex,
                    SavedAt = DateTime.UtcNow,
                    Messages = trimmedConversation.Select(m => new CheckpointMessage
                    {
                        Role = m.Role,
                        Content = m.Content != null
                            ? JsonSerializer.SerializeToElement(m.Content, _jsonOptions)
                            : null
                    }).ToList()
                };

                var checkpointPath = GetCheckpointPath(plan.ProjectId, plan.PlanFilename);
                var json = JsonSerializer.Serialize(checkpoint, _jsonOptions);
                await File.WriteAllTextAsync(checkpointPath, json);

                _logger?.LogDebug(
                    "Saved conversation checkpoint for task {TaskId} at step {StepIndex} ({MessageCount} messages)",
                    plan.TaskId[..Math.Min(8, plan.TaskId.Length)], plan.CurrentStepIndex, checkpoint.Messages.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to save conversation checkpoint for task {TaskId}", plan.TaskId);
            }
        }

        /// <summary>
        /// Loads a conversation checkpoint for a plan.
        /// Returns null if no checkpoint exists or if loading fails.
        /// </summary>
        public async Task<ConversationCheckpoint?> LoadConversationCheckpointAsync(string projectId, string taskId)
        {
            try
            {
                // Look up plan filename from index
                var index = await LoadPlanIndexAsync(projectId);
                if (!index.TryGetValue(taskId, out var planFilename))
                {
                    return null;
                }

                var checkpointPath = GetCheckpointPath(projectId, planFilename);
                if (!File.Exists(checkpointPath))
                {
                    return null;
                }

                var json = await File.ReadAllTextAsync(checkpointPath);
                var checkpoint = JsonSerializer.Deserialize<ConversationCheckpoint>(json, _jsonOptions);

                if (checkpoint != null)
                {
                    _logger?.LogDebug(
                        "Loaded conversation checkpoint for task {TaskId} at step {StepIndex} ({MessageCount} messages)",
                        taskId[..Math.Min(8, taskId.Length)], checkpoint.StepIndex, checkpoint.Messages.Count);
                }

                return checkpoint;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load conversation checkpoint for task {TaskId}", taskId);
                return null;
            }
        }

        /// <summary>
        /// Restores a conversation from a checkpoint, converting CheckpointMessages back to Messages.
        /// </summary>
        public static List<Message> RestoreConversation(ConversationCheckpoint checkpoint)
        {
            return checkpoint.Messages.Select(m => new Message
            {
                Role = m.Role,
                Content = m.Content.HasValue ? (object)m.Content.Value : null
            }).ToList();
        }
    }
}
