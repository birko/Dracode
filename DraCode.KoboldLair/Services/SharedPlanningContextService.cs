using System.Collections.Concurrent;
using System.Text.Json;
using DraCode.KoboldLair.Models.Agents;

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
    /// Extracts or infers file purpose from plan steps
    /// </summary>
    public string InferFilePurpose(string filePath, KoboldImplementationPlan? plan)
    {
        if (plan == null) return "Unknown purpose";

        // Look through steps to find descriptions mentioning this file
        foreach (var step in plan.Steps)
        {
            if (step.FilesToCreate.Contains(filePath) || step.FilesToModify.Contains(filePath))
            {
                // Use step description as purpose
                return step.Description.Length > 200 
                    ? step.Description.Substring(0, 200) + "..."
                    : step.Description;
            }
        }

        // Fallback to file name analysis
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        return $"{fileName} - {GetFileCategoryFromPath(filePath)}";
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
                // Record created files
                foreach (var file in step.FilesToCreate)
                {
                    await UpdateFileMetadataAsync(
                        agentContext.ProjectId,
                        file,
                        step.Description,
                        agentContext.TaskId,
                        isCreation: true);
                }

                // Record modified files
                foreach (var file in step.FilesToModify)
                {
                    await UpdateFileMetadataAsync(
                        agentContext.ProjectId,
                        file,
                        step.Description,
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
