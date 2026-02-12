using System.Collections.Concurrent;
using System.Text.Json;
using DraCode.KoboldLair.Models.Agents;
// Note: ReflectionSignal is in DraCode.KoboldLair.Models.Agents namespace

namespace DraCode.KoboldLair.Services;

/// <summary>
/// Service for sharing planning context between multiple Kobolds, Drake supervisors, and across projects.
/// Enables coordination, learning from past executions, and intelligent task distribution.
/// Thread-safe for concurrent access from multiple agents.
/// </summary>
public class SharedPlanningContextService
{
    private readonly KoboldPlanService _planService;
    private readonly ProjectRepository _projectRepository;
    private readonly ILogger<SharedPlanningContextService>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _projectsPath;

    // In-memory caches for fast access
    private readonly ConcurrentDictionary<string, ProjectPlanningContext> _projectContexts = new();
    private readonly ConcurrentDictionary<string, AgentPlanningContext> _activeAgentContexts = new();
    private readonly ConcurrentDictionary<string, PlanningInsight> _insights = new();
    private readonly SemaphoreSlim _persistenceLock = new(1, 1);

    // Configuration
    private const int MaxInsightsPerProject = 100;
    private const int MaxCachedProjects = 50;

    public SharedPlanningContextService(
        string projectsPath,
        KoboldPlanService planService,
        ProjectRepository projectRepository,
        ILogger<SharedPlanningContextService>? logger = null)
    {
        _projectsPath = projectsPath;
        _planService = planService;
        _projectRepository = projectRepository;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    #region Project Context Management

    /// <summary>
    /// Gets or creates the planning context for a project
    /// </summary>
    public async Task<ProjectPlanningContext> GetProjectContextAsync(string projectId)
    {
        if (_projectContexts.TryGetValue(projectId, out var context))
        {
            context.LastAccessedAt = DateTime.UtcNow;
            return context;
        }

        // Load from disk or create new
        context = await LoadProjectContextAsync(projectId) ?? new ProjectPlanningContext
        {
            ProjectId = projectId,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow
        };

        _projectContexts[projectId] = context;
        await TrimCacheIfNeededAsync();
        return context;
    }

    /// <summary>
    /// Registers an active agent working on a task
    /// </summary>
    public async Task RegisterAgentAsync(string agentId, string projectId, string taskId, string agentType)
    {
        var agentContext = new AgentPlanningContext
        {
            AgentId = agentId,
            ProjectId = projectId,
            TaskId = taskId,
            AgentType = agentType,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        _activeAgentContexts[agentId] = agentContext;

        var projectContext = await GetProjectContextAsync(projectId);
        projectContext.ActiveAgentCount++;
        projectContext.ActiveAgents[agentId] = taskId;

        _logger?.LogDebug("Registered agent {AgentId} ({AgentType}) for task {TaskId} in project {ProjectId}",
            agentId[..8], agentType, taskId[..8], projectId);
    }

    /// <summary>
    /// Unregisters an agent when it completes or fails
    /// </summary>
    public async Task UnregisterAgentAsync(string agentId, bool success, string? errorMessage = null)
    {
        if (!_activeAgentContexts.TryRemove(agentId, out var agentContext))
        {
            return;
        }

        var projectContext = await GetProjectContextAsync(agentContext.ProjectId);
        projectContext.ActiveAgentCount = Math.Max(0, projectContext.ActiveAgentCount - 1);
        projectContext.ActiveAgents.TryRemove(agentId, out _);

        if (success)
        {
            projectContext.CompletedTasksCount++;
        }
        else
        {
            projectContext.FailedTasksCount++;
        }

        agentContext.CompletedAt = DateTime.UtcNow;
        agentContext.Success = success;
        agentContext.ErrorMessage = errorMessage;

        // Record completion for learning
        await RecordTaskCompletionAsync(agentContext);

        _logger?.LogDebug("Unregistered agent {AgentId} - Success: {Success}",
            agentId[..8], success);
    }

    /// <summary>
    /// Updates agent activity timestamp (for heartbeat/monitoring)
    /// </summary>
    public void UpdateAgentActivity(string agentId)
    {
        if (_activeAgentContexts.TryGetValue(agentId, out var context))
        {
            context.LastActivityAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Gets all active agents for a project
    /// </summary>
    public async Task<List<AgentPlanningContext>> GetActiveAgentsAsync(string projectId)
    {
        var projectContext = await GetProjectContextAsync(projectId);
        return _activeAgentContexts.Values
            .Where(a => a.ProjectId == projectId)
            .ToList();
    }

    #endregion

    #region Plan Coordination

    /// <summary>
    /// Gets related plans that might be helpful for the current task
    /// </summary>
    public async Task<List<KoboldImplementationPlan>> GetRelatedPlansAsync(
        string projectId,
        string currentTaskId,
        IEnumerable<string> relatedFiles)
    {
        var allPlans = await _planService.GetPlansForProjectAsync(projectId);
        var relatedFilesList = relatedFiles.ToList();

        return allPlans
            .Where(p => p.TaskId != currentTaskId)
            .Where(p => p.Status == PlanStatus.Completed || p.Status == PlanStatus.InProgress)
            .Where(p => HasFileOverlap(p, relatedFilesList))
            .OrderByDescending(p => CalculateRelevanceScore(p, relatedFilesList))
            .Take(5)
            .ToList();
    }

    /// <summary>
    /// Checks if a file is currently being modified by another agent
    /// </summary>
    public async Task<bool> IsFileInUseAsync(string projectId, string filePath)
    {
        var activeAgents = await GetActiveAgentsAsync(projectId);
        
        foreach (var agent in activeAgents)
        {
            var plan = await _planService.LoadPlanAsync(agent.ProjectId, agent.TaskId);
            if (plan?.CurrentStep != null)
            {
                if (plan.CurrentStep.FilesToCreate.Contains(filePath) ||
                    plan.CurrentStep.FilesToModify.Contains(filePath))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Gets files currently being modified across all agents in a project
    /// </summary>
    public async Task<HashSet<string>> GetFilesInUseAsync(string projectId)
    {
        var filesInUse = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activeAgents = await GetActiveAgentsAsync(projectId);

        foreach (var agent in activeAgents)
        {
            var plan = await _planService.LoadPlanAsync(agent.ProjectId, agent.TaskId);
            if (plan?.CurrentStep != null)
            {
                filesInUse.UnionWith(plan.CurrentStep.FilesToCreate);
                filesInUse.UnionWith(plan.CurrentStep.FilesToModify);
            }
        }

        return filesInUse;
    }

    /// <summary>
    /// Gets file metadata with purposes for workspace context
    /// </summary>
    public async Task<Dictionary<string, FileMetadata>> GetFileMetadataAsync(string projectId)
    {
        var projectContext = await GetProjectContextAsync(projectId);
        return projectContext.FileRegistry.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Updates file metadata when a file is created or modified
    /// </summary>
    public async Task UpdateFileMetadataAsync(
        string projectId,
        string filePath,
        string purpose,
        string taskId,
        bool isCreation)
    {
        var projectContext = await GetProjectContextAsync(projectId);
        
        if (!projectContext.FileRegistry.TryGetValue(filePath, out var metadata))
        {
            metadata = new FileMetadata
            {
                FilePath = filePath,
                Purpose = purpose,
                Category = GetFileCategoryFromPath(filePath),
                FirstCreated = DateTime.UtcNow,
                LastModified = DateTime.UtcNow
            };
            projectContext.FileRegistry[filePath] = metadata;
        }
        else
        {
            // Update existing metadata
            if (!string.IsNullOrWhiteSpace(purpose))
            {
                metadata.Purpose = purpose;
            }
            metadata.LastModified = DateTime.UtcNow;
        }

        // Track which tasks touched this file
        if (isCreation && !metadata.CreatedByTasks.Contains(taskId))
        {
            metadata.CreatedByTasks.Add(taskId);
        }
        else if (!isCreation && !metadata.ModifiedByTasks.Contains(taskId))
        {
            metadata.ModifiedByTasks.Add(taskId);
        }

        await PersistProjectContextAsync(projectId);
    }

    /// <summary>
    /// Generates a meaningful purpose description for a file based on its path, step context, and task
    /// </summary>
    public string GenerateFilePurpose(
        string filePath,
        ImplementationStep step,
        string taskDescription,
        bool isCreation)
    {
        var fileName = Path.GetFileName(filePath);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var directory = Path.GetDirectoryName(filePath) ?? "";
        var category = GetFileCategoryFromPath(filePath);

        // Build a purpose based on file characteristics and context
        var purposeParts = new List<string>();

        // 1. Determine file type/role from naming conventions
        var fileRole = InferFileRole(fileNameWithoutExt, extension, directory);
        if (!string.IsNullOrEmpty(fileRole))
        {
            purposeParts.Add(fileRole);
        }

        // 2. Add action context (created vs modified)
        var action = isCreation ? "Created" : "Modified";

        // 3. Extract relevant context from step title (usually more concise than description)
        var stepContext = ExtractRelevantContext(step.Title, fileName);
        if (!string.IsNullOrEmpty(stepContext))
        {
            purposeParts.Add(stepContext);
        }

        // 4. If still minimal, try to extract from task description
        if (purposeParts.Count < 2 && !string.IsNullOrEmpty(taskDescription))
        {
            var taskContext = ExtractTaskContext(taskDescription);
            if (!string.IsNullOrEmpty(taskContext))
            {
                purposeParts.Add($"for {taskContext}");
            }
        }

        // Build final purpose string
        if (purposeParts.Count == 0)
        {
            return $"{category} - {action} in step: {TruncateText(step.Title, 100)}";
        }

        var purpose = string.Join(" - ", purposeParts);
        return TruncateText(purpose, 200);
    }

    /// <summary>
    /// Infers the role/purpose of a file from its name and location
    /// </summary>
    private string InferFileRole(string fileNameWithoutExt, string extension, string directory)
    {
        var nameLower = fileNameWithoutExt.ToLowerInvariant();
        var dirLower = directory.ToLowerInvariant();

        // Check for common patterns in file name
        if (nameLower.StartsWith("i") && char.IsUpper(fileNameWithoutExt.ElementAtOrDefault(1)))
            return "Interface definition";
        if (nameLower.EndsWith("service")) return "Service implementation";
        if (nameLower.EndsWith("controller")) return "API controller";
        if (nameLower.EndsWith("repository")) return "Data repository";
        if (nameLower.EndsWith("handler")) return "Event/request handler";
        if (nameLower.EndsWith("factory")) return "Factory class";
        if (nameLower.EndsWith("provider")) return "Provider implementation";
        if (nameLower.EndsWith("helper") || nameLower.EndsWith("utils")) return "Utility functions";
        if (nameLower.EndsWith("config") || nameLower.EndsWith("configuration")) return "Configuration";
        if (nameLower.EndsWith("test") || nameLower.EndsWith("tests") || nameLower.EndsWith("spec")) return "Test file";
        if (nameLower.EndsWith("model") || nameLower.EndsWith("dto") || nameLower.EndsWith("entity")) return "Data model";
        if (nameLower.EndsWith("middleware")) return "Middleware component";
        if (nameLower.EndsWith("extension") || nameLower.EndsWith("extensions")) return "Extension methods";
        if (nameLower.Contains("component")) return "UI component";
        if (nameLower.Contains("hook")) return "React hook";
        if (nameLower.Contains("context")) return "Context provider";
        if (nameLower.Contains("reducer")) return "State reducer";
        if (nameLower.Contains("action")) return "Action definitions";
        if (nameLower.Contains("store")) return "State store";
        if (nameLower.Contains("route") || nameLower.Contains("router")) return "Routing configuration";
        if (nameLower.Contains("style") || nameLower.Contains("theme")) return "Styling/theme";
        if (nameLower.Contains("constant") || nameLower.Contains("const")) return "Constants/enums";
        if (nameLower.Contains("type") && extension is ".ts" or ".tsx") return "Type definitions";
        if (nameLower == "index") return "Module index/exports";
        if (nameLower == "program" || nameLower == "main" || nameLower == "app") return "Application entry point";
        if (nameLower == "startup") return "Application startup configuration";

        // Check directory patterns
        if (dirLower.Contains("models") || dirLower.Contains("entities")) return "Data model";
        if (dirLower.Contains("services")) return "Service layer";
        if (dirLower.Contains("controllers") || dirLower.Contains("api")) return "API endpoint";
        if (dirLower.Contains("components")) return "UI component";
        if (dirLower.Contains("hooks")) return "Custom hook";
        if (dirLower.Contains("utils") || dirLower.Contains("helpers")) return "Utility module";
        if (dirLower.Contains("tests") || dirLower.Contains("__tests__")) return "Test file";
        if (dirLower.Contains("config") || dirLower.Contains("configuration")) return "Configuration";
        if (dirLower.Contains("middleware")) return "Middleware";
        if (dirLower.Contains("views") || dirLower.Contains("pages")) return "View/page";
        if (dirLower.Contains("styles") || dirLower.Contains("css")) return "Stylesheet";

        // Check extension-specific defaults
        return extension switch
        {
            ".cs" => "C# source file",
            ".csproj" => "Project configuration",
            ".sln" or ".slnx" => "Solution file",
            ".json" when nameLower.Contains("package") => "Package manifest",
            ".json" when nameLower.Contains("tsconfig") => "TypeScript configuration",
            ".json" => "JSON data/config",
            ".ts" or ".tsx" => "TypeScript module",
            ".js" or ".jsx" => "JavaScript module",
            ".css" or ".scss" or ".sass" or ".less" => "Stylesheet",
            ".html" => "HTML template",
            ".md" => "Documentation",
            ".sql" => "Database script",
            ".yaml" or ".yml" => "YAML configuration",
            ".xml" => "XML configuration",
            ".env" => "Environment variables",
            ".gitignore" => "Git ignore rules",
            ".dockerfile" or ".docker" => "Docker configuration",
            _ => ""
        };
    }

    /// <summary>
    /// Extracts relevant context from step title, avoiding redundancy with file name
    /// </summary>
    private string ExtractRelevantContext(string stepTitle, string fileName)
    {
        if (string.IsNullOrWhiteSpace(stepTitle)) return "";

        // Remove common step prefixes
        var cleaned = stepTitle
            .Replace("Create ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Implement ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Add ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Update ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Modify ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Set up ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Configure ", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        // If the step title is just about the file, skip it
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (cleaned.Equals(fileNameWithoutExt, StringComparison.OrdinalIgnoreCase) ||
            cleaned.Equals(fileName, StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return TruncateText(cleaned, 100);
    }

    /// <summary>
    /// Extracts a concise context from the task description
    /// </summary>
    private string ExtractTaskContext(string taskDescription)
    {
        if (string.IsNullOrWhiteSpace(taskDescription)) return "";

        // Take first sentence or first 50 chars
        var firstSentenceEnd = taskDescription.IndexOfAny(new[] { '.', '!', '?' });
        var context = firstSentenceEnd > 0 && firstSentenceEnd < 80
            ? taskDescription[..firstSentenceEnd]
            : TruncateText(taskDescription, 60);

        // Clean up common task prefixes
        context = context
            .Replace("Implement ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Create ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Add ", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Build ", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        return context;
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Extracts or infers file purpose from plan steps (legacy method for backward compatibility)
    /// </summary>
    public string InferFilePurpose(string filePath, KoboldImplementationPlan? plan)
    {
        if (plan == null) return $"{GetFileCategoryFromPath(filePath)} - Unknown purpose";

        // Look through steps to find the step that touches this file
        foreach (var step in plan.Steps)
        {
            var isCreation = step.FilesToCreate.Contains(filePath);
            var isModification = step.FilesToModify.Contains(filePath);

            if (isCreation || isModification)
            {
                return GenerateFilePurpose(filePath, step, plan.TaskDescription, isCreation);
            }
        }

        // Fallback to basic file analysis
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var role = InferFileRole(fileName, Path.GetExtension(filePath), Path.GetDirectoryName(filePath) ?? "");
        return !string.IsNullOrEmpty(role)
            ? role
            : $"{GetFileCategoryFromPath(filePath)} - {fileName}";
    }

    private string GetFileCategoryFromPath(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".cs" => "C# Source",
            ".csproj" => "Project File",
            ".sln" or ".slnx" => "Solution",
            ".json" => "Configuration",
            ".js" or ".ts" => "JavaScript/TypeScript",
            ".jsx" or ".tsx" => "React Component",
            ".css" or ".scss" or ".sass" => "Stylesheet",
            ".html" or ".htm" => "HTML",
            ".md" => "Documentation",
            _ => "File"
        };
    }

    #endregion

    #region Reflection Recording

    /// <summary>
    /// Records a reflection checkpoint from a Kobold for pattern analysis and learning.
    /// This data is used by ReasoningMonitorService to detect concerning patterns.
    /// </summary>
    public async Task RecordReflectionAsync(string projectId, string taskId, ReflectionSignal reflection)
    {
        var projectContext = await GetProjectContextAsync(projectId);

        // Initialize reflection tracking if needed
        if (!projectContext.ReflectionsByTask.ContainsKey(taskId))
        {
            projectContext.ReflectionsByTask[taskId] = new List<ReflectionSignal>();
        }

        projectContext.ReflectionsByTask[taskId].Add(reflection);

        // Keep only the last 50 reflections per task to prevent unbounded growth
        if (projectContext.ReflectionsByTask[taskId].Count > 50)
        {
            projectContext.ReflectionsByTask[taskId] = projectContext.ReflectionsByTask[taskId]
                .TakeLast(50)
                .ToList();
        }

        _logger?.LogDebug(
            "Recorded reflection for task {TaskId} in project {ProjectId}: {Progress}% progress, {Confidence}% confidence, {Decision}",
            taskId[..Math.Min(8, taskId.Length)], projectId, reflection.ProgressPercent, reflection.Confidence, reflection.Decision);

        // Persist changes
        await PersistProjectContextAsync(projectId);
    }

    /// <summary>
    /// Gets all reflections for a specific task
    /// </summary>
    public async Task<List<ReflectionSignal>> GetTaskReflectionsAsync(string projectId, string taskId)
    {
        var projectContext = await GetProjectContextAsync(projectId);

        if (projectContext.ReflectionsByTask.TryGetValue(taskId, out var reflections))
        {
            return reflections.ToList();
        }

        return new List<ReflectionSignal>();
    }

    /// <summary>
    /// Gets recent reflections across all tasks in a project (for monitoring dashboard)
    /// </summary>
    public async Task<List<(string TaskId, ReflectionSignal Reflection)>> GetRecentReflectionsAsync(
        string projectId,
        int maxResults = 20)
    {
        var projectContext = await GetProjectContextAsync(projectId);

        return projectContext.ReflectionsByTask
            .SelectMany(kvp => kvp.Value.Select(r => (TaskId: kvp.Key, Reflection: r)))
            .OrderByDescending(x => x.Reflection.Timestamp)
            .Take(maxResults)
            .ToList();
    }

    #endregion

    #region Learning & Insights

    /// <summary>
    /// Records a task completion for learning purposes
    /// </summary>
    private async Task RecordTaskCompletionAsync(AgentPlanningContext agentContext)
    {
        var plan = await _planService.LoadPlanAsync(agentContext.ProjectId, agentContext.TaskId);
        if (plan == null) return;

        var insight = new PlanningInsight
        {
            InsightId = Guid.NewGuid().ToString(),
            ProjectId = agentContext.ProjectId,
            TaskId = agentContext.TaskId,
            AgentType = agentContext.AgentType,
            Timestamp = DateTime.UtcNow,
            Success = agentContext.Success,
            DurationSeconds = (agentContext.CompletedAt!.Value - agentContext.StartedAt).TotalSeconds,
            StepCount = plan.Steps.Count,
            CompletedSteps = plan.CompletedStepsCount,
            TotalIterations = plan.Steps.Sum(s => s.Metrics.IterationsUsed),
            FilesModified = plan.Steps.SelectMany(s => s.FilesToModify).Distinct().Count(),
            FilesCreated = plan.Steps.SelectMany(s => s.FilesToCreate).Distinct().Count(),
            ErrorMessage = agentContext.ErrorMessage
        };

        _insights[insight.InsightId] = insight;

        var projectContext = await GetProjectContextAsync(agentContext.ProjectId);
        projectContext.Insights.Add(insight);

        // Update file metadata from completed plan
        if (agentContext.Success && plan.Status == PlanStatus.Completed)
        {
            foreach (var step in plan.Steps.Where(s => s.Status == StepStatus.Completed))
            {
                // Record created files with meaningful purpose
                foreach (var file in step.FilesToCreate)
                {
                    var purpose = GenerateFilePurpose(file, step, plan.TaskDescription, isCreation: true);
                    await UpdateFileMetadataAsync(
                        agentContext.ProjectId,
                        file,
                        purpose,
                        agentContext.TaskId,
                        isCreation: true);
                }

                // Record modified files with meaningful purpose
                foreach (var file in step.FilesToModify)
                {
                    var purpose = GenerateFilePurpose(file, step, plan.TaskDescription, isCreation: false);
                    await UpdateFileMetadataAsync(
                        agentContext.ProjectId,
                        file,
                        purpose,
                        agentContext.TaskId,
                        isCreation: false);
                }
            }
        }

        // Trim insights if too many
        if (projectContext.Insights.Count > MaxInsightsPerProject)
        {
            var toRemove = projectContext.Insights
                .OrderBy(i => i.Timestamp)
                .Take(projectContext.Insights.Count - MaxInsightsPerProject)
                .ToList();

            foreach (var i in toRemove)
            {
                projectContext.Insights.Remove(i);
                _insights.TryRemove(i.InsightId, out _);
            }
        }

        await PersistProjectContextAsync(agentContext.ProjectId);
    }

    /// <summary>
    /// Gets insights for similar tasks to help with planning
    /// </summary>
    public async Task<List<PlanningInsight>> GetSimilarTaskInsightsAsync(
        string projectId,
        string agentType,
        int maxResults = 10)
    {
        var projectContext = await GetProjectContextAsync(projectId);
        
        return projectContext.Insights
            .Where(i => i.AgentType == agentType && i.Success)
            .OrderByDescending(i => i.Timestamp)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Gets aggregate statistics for a project
    /// </summary>
    public async Task<PlanningStatistics> GetProjectStatisticsAsync(string projectId)
    {
        var projectContext = await GetProjectContextAsync(projectId);
        var successfulInsights = projectContext.Insights.Where(i => i.Success).ToList();

        return new PlanningStatistics
        {
            ProjectId = projectId,
            TotalTasksCompleted = projectContext.CompletedTasksCount,
            TotalTasksFailed = projectContext.FailedTasksCount,
            CurrentlyActive = projectContext.ActiveAgentCount,
            AverageDurationSeconds = successfulInsights.Any()
                ? successfulInsights.Average(i => i.DurationSeconds)
                : 0,
            AverageStepsPerTask = successfulInsights.Any()
                ? successfulInsights.Average(i => i.StepCount)
                : 0,
            AverageIterationsPerTask = successfulInsights.Any()
                ? successfulInsights.Average(i => i.TotalIterations)
                : 0,
            SuccessRate = projectContext.CompletedTasksCount + projectContext.FailedTasksCount > 0
                ? (double)projectContext.CompletedTasksCount / (projectContext.CompletedTasksCount + projectContext.FailedTasksCount) * 100
                : 0,
            MostActiveAgentType = projectContext.Insights
                .GroupBy(i => i.AgentType)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? "unknown"
        };
    }

    #endregion

    #region Cross-Project Learning

    /// <summary>
    /// Gets insights from other projects that might be helpful
    /// </summary>
    public async Task<List<PlanningInsight>> GetCrossProjectInsightsAsync(
        string currentProjectId,
        string agentType,
        int maxResults = 5)
    {
        var insights = new List<PlanningInsight>();

        // Get insights from all cached projects except current
        foreach (var kvp in _projectContexts.Where(p => p.Key != currentProjectId))
        {
            insights.AddRange(kvp.Value.Insights
                .Where(i => i.AgentType == agentType && i.Success)
                .OrderByDescending(i => i.Timestamp)
                .Take(3));
        }

        return insights
            .OrderByDescending(i => i.Timestamp)
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Gets best practices/patterns learned across all projects
    /// </summary>
    public async Task<Dictionary<string, string>> GetBestPracticesAsync(string agentType)
    {
        var practices = new Dictionary<string, string>();
        var agentInsights = _insights.Values
            .Where(i => i.AgentType == agentType && i.Success)
            .ToList();

        if (!agentInsights.Any()) return practices;

        // Analyze patterns
        var avgSteps = agentInsights.Average(i => i.StepCount);
        var avgIterations = agentInsights.Average(i => i.TotalIterations);
        var avgDuration = agentInsights.Average(i => i.DurationSeconds);

        practices["typical_steps"] = $"{avgSteps:F1} steps on average";
        practices["typical_iterations"] = $"{avgIterations:F1} iterations per task";
        practices["typical_duration"] = $"{avgDuration:F0} seconds average";
        practices["success_rate"] = $"{(double)agentInsights.Count / _insights.Values.Count(i => i.AgentType == agentType) * 100:F1}%";

        return practices;
    }

    #endregion

    #region Persistence

    private string GetContextFilePath(string projectId)
    {
        var project = _projectRepository.GetById(projectId);
        if (project == null) return string.Empty;

        var outputPath = project.Paths.Output;
        if (!Path.IsPathRooted(outputPath))
        {
            outputPath = Path.Combine(_projectsPath, outputPath);
        }

        return Path.Combine(outputPath, "planning-context.json");
    }

    private async Task<ProjectPlanningContext?> LoadProjectContextAsync(string projectId)
    {
        var path = GetContextFilePath(projectId);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<ProjectPlanningContext>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load planning context from {Path}", path);
            return null;
        }
    }

    private async Task PersistProjectContextAsync(string projectId)
    {
        if (!_projectContexts.TryGetValue(projectId, out var context))
        {
            return;
        }

        await _persistenceLock.WaitAsync();
        try
        {
            var path = GetContextFilePath(projectId);
            if (string.IsNullOrEmpty(path)) return;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(context, _jsonOptions);
            await File.WriteAllTextAsync(path, json);

            _logger?.LogDebug("Persisted planning context for project {ProjectId}", projectId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to persist planning context for project {ProjectId}", projectId);
        }
        finally
        {
            _persistenceLock.Release();
        }
    }

    /// <summary>
    /// Persists all cached contexts to disk
    /// </summary>
    public async Task PersistAllContextsAsync()
    {
        var tasks = _projectContexts.Keys
            .Select(projectId => PersistProjectContextAsync(projectId))
            .ToList();

        await Task.WhenAll(tasks);
    }

    private async Task TrimCacheIfNeededAsync()
    {
        if (_projectContexts.Count <= MaxCachedProjects)
        {
            return;
        }

        // Remove least recently accessed contexts
        var toRemove = _projectContexts
            .OrderBy(kvp => kvp.Value.LastAccessedAt)
            .Take(_projectContexts.Count - MaxCachedProjects)
            .ToList();

        foreach (var kvp in toRemove)
        {
            await PersistProjectContextAsync(kvp.Key);
            _projectContexts.TryRemove(kvp.Key, out _);
        }
    }

    #endregion

    #region Helper Methods

    private bool HasFileOverlap(KoboldImplementationPlan plan, List<string> files)
    {
        var planFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in plan.Steps)
        {
            planFiles.UnionWith(step.FilesToCreate);
            planFiles.UnionWith(step.FilesToModify);
        }

        return files.Any(f => planFiles.Contains(f));
    }

    private double CalculateRelevanceScore(KoboldImplementationPlan plan, List<string> files)
    {
        var planFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in plan.Steps)
        {
            planFiles.UnionWith(step.FilesToCreate);
            planFiles.UnionWith(step.FilesToModify);
        }

        var overlap = files.Count(f => planFiles.Contains(f));
        var recency = 1.0 / (1.0 + (DateTime.UtcNow - plan.UpdatedAt).TotalHours);
        
        return overlap * 10 + recency;
    }

    #endregion
}

#region Context Models

/// <summary>
/// Planning context for a project
/// </summary>
public class ProjectPlanningContext
{
    public string ProjectId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public int ActiveAgentCount { get; set; }
    public int CompletedTasksCount { get; set; }
    public int FailedTasksCount { get; set; }
    public ConcurrentDictionary<string, string> ActiveAgents { get; set; } = new();
    public List<PlanningInsight> Insights { get; set; } = new();
    public ConcurrentDictionary<string, FileMetadata> FileRegistry { get; set; } = new();

    /// <summary>
    /// Reflection history by task ID for pattern analysis and monitoring
    /// </summary>
    public Dictionary<string, List<ReflectionSignal>> ReflectionsByTask { get; set; } = new();
}

/// <summary>
/// Metadata about a file in the workspace
/// </summary>
public class FileMetadata
{
    public string FilePath { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime FirstCreated { get; set; }
    public DateTime LastModified { get; set; }
    public List<string> CreatedByTasks { get; set; } = new();
    public List<string> ModifiedByTasks { get; set; } = new();
    public string? LastKnownContent { get; set; }
    public int LineCount { get; set; }
}

/// <summary>
/// Context for an active agent
/// </summary>
public class AgentPlanningContext
{
    public string AgentId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Learning insight from completed task
/// </summary>
public class PlanningInsight
{
    public string InsightId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string AgentType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public double DurationSeconds { get; set; }
    public int StepCount { get; set; }
    public int CompletedSteps { get; set; }
    public int TotalIterations { get; set; }
    public int FilesModified { get; set; }
    public int FilesCreated { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Aggregate statistics for a project
/// </summary>
public class PlanningStatistics
{
    public string ProjectId { get; set; } = string.Empty;
    public int TotalTasksCompleted { get; set; }
    public int TotalTasksFailed { get; set; }
    public int CurrentlyActive { get; set; }
    public double AverageDurationSeconds { get; set; }
    public double AverageStepsPerTask { get; set; }
    public double AverageIterationsPerTask { get; set; }
    public double SuccessRate { get; set; }
    public string MostActiveAgentType { get; set; } = string.Empty;
}

#endregion
