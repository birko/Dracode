using System.Text.Json;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services;

/// <summary>
/// Service for managing project implementation summaries.
/// Provides cross-task context and tracks specification-to-code impact.
/// </summary>
public class ProjectImplementationService
{
    private readonly string _projectsPath;
    private readonly ProjectRepository _projectRepository;
    private readonly ILogger<ProjectImplementationService>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // In-memory cache of summaries
    private readonly Dictionary<string, ProjectImplementationSummary> _summaries = new();

    public ProjectImplementationService(
        string projectsPath,
        ProjectRepository projectRepository,
        ILogger<ProjectImplementationService>? logger = null)
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
    /// Gets or creates the implementation summary for a project
    /// </summary>
    public async Task<ProjectImplementationSummary> GetOrCreateSummaryAsync(string projectId)
    {
        if (_summaries.TryGetValue(projectId, out var cached))
        {
            return cached;
        }

        await _lock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_summaries.TryGetValue(projectId, out cached))
            {
                return cached;
            }

            var project = _projectRepository.GetById(projectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project {projectId} not found");
            }

            var outputPath = GetProjectOutputPath(project);
            var summary = await ProjectImplementationSummary.LoadAsync(outputPath);

            if (summary == null)
            {
                // Create new summary
                summary = new ProjectImplementationSummary
                {
                    ProjectId = projectId,
                    ProjectName = project.Name
                };

                // Initialize with specification if available
                var specPath = project.Paths.Specification;
                if (!string.IsNullOrEmpty(specPath) && File.Exists(specPath))
                {
                    var spec = await LoadSpecificationAsync(specPath, projectId);
                    if (spec != null)
                    {
                        InitializeFromSpecification(summary, spec);
                    }
                }
            }

            _summaries[projectId] = summary;
            return summary;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Initializes or updates the summary from a specification
    /// </summary>
    public async Task<ProjectImplementationSummary> InitializeFromSpecificationAsync(
        string projectId,
        Specification specification)
    {
        var summary = await GetOrCreateSummaryAsync(projectId);

        // Check if specification changed
        if (summary.HasSpecificationChanged(specification))
        {
            _logger?.LogInformation(
                "Specification changed for project {ProjectId} (v{OldVersion} -> v{NewVersion}), reinitializing summary",
                projectId, summary.SpecificationVersion, specification.Version);

            // Clear existing feature implementations that may be stale
            var featuresToKeep = new HashSet<string>(
                specification.Features.Select(f => f.Id));
            var staleEntries = summary.FeatureImplementations
                .Where(kvp => !featuresToKeep.Contains(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var staleKey in staleEntries)
            {
                summary.FeatureImplementations.Remove(staleKey);
            }
        }

        InitializeFromSpecification(summary, specification);
        await SaveSummaryAsync(summary);

        return summary;

    }

    /// <summary>
    /// Checks for specification changes and regenerates summaries if needed.
    /// Called by Dragon or Wyvern when specification is updated.
    /// </summary>
    public async Task<bool> CheckAndRegenerateOnSpecChangeAsync(string projectId, Specification currentSpecification)
    {
        var summary = await GetOrCreateSummaryAsync(projectId);
        
        if (summary.HasSpecificationChanged(currentSpecification))
        {
            _logger?.LogInformation(
                "Specification change detected for project {ProjectId} - regenerating implementation summary (v{OldVersion} -> v{NewVersion})",
                projectId, summary.SpecificationVersion, currentSpecification.Version);

            await InitializeFromSpecificationAsync(projectId, currentSpecification);
            return true;
        }
        
        return false;
    }

    /// <summary>
    /// Gets the current specification version for a project.
    /// Used to detect when Kobolds need to reload their context.
    /// </summary>
    public async Task<(int version, string contentHash)> GetSpecificationVersionAsync(string projectId)
    {
        var summary = await GetOrCreateSummaryAsync(projectId);
        return (summary.SpecificationVersion, summary.SpecificationContentHash);
    }

    /// <summary>
    /// Validates if a Kobold's plan is still valid with current specification.
    /// Returns false if specification has changed since plan was created.
    /// </summary>
    public async Task<bool> ValidatePlanSpecificationAsync(string projectId, KoboldImplementationPlan plan)
    {
        var summary = await GetOrCreateSummaryAsync(projectId);
        
        // Check if specification version matches
        if (plan.SpecificationVersion != summary.SpecificationVersion ||
            plan.SpecificationContentHash != summary.SpecificationContentHash)
        {
            _logger?.LogWarning(
                "Plan for task {TaskId} has stale specification (plan v{PlanVersion} != current v{CurrentVersion})",
                plan.TaskId, plan.SpecificationVersion, summary.SpecificationVersion);
            return false;
        }
        
        return true;
    }


    /// <summary>
    /// Updates the summary with completed task information
    /// </summary>
    public async Task RecordTaskCompletionAsync(
        string projectId,
        string taskId,
        string? featureId,
        KoboldImplementationPlan plan,
        bool success)
    {
        var summary = await GetOrCreateSummaryAsync(projectId);

        if (!success)
        {
            // Mark feature as failed if appropriate
            if (!string.IsNullOrEmpty(featureId) &&
                summary.FeatureImplementations.TryGetValue(featureId, out var featureImpl))
            {
                featureImpl.Status = FeatureImplementationStatus.Failed;
                featureImpl.LastActivityAt = DateTime.UtcNow;
            }
            await SaveSummaryAsync(summary);
            return;
        }

        // Record all completed steps
        foreach (var step in plan.Steps.Where(s => s.Status == StepStatus.Completed))
        {
            summary.RecordStepCompletion(taskId, featureId ?? "", step);
        }

        // Update task-to-feature mapping
        if (!string.IsNullOrEmpty(featureId))
        {
            summary.TaskToFeatureMap[taskId] = featureId;

            if (summary.FeatureImplementations.TryGetValue(featureId, out var featureImpl))
            {
                if (!featureImpl.TaskIds.Contains(taskId))
                {
                    featureImpl.TaskIds.Add(taskId);
                }

                // Update status
                var totalSteps = featureImpl.TotalSteps + plan.Steps.Count;
                var completedSteps = featureImpl.CompletedSteps + plan.CompletedStepsCount;
                featureImpl.TotalSteps = totalSteps;
                featureImpl.CompletedSteps = completedSteps;

                if (completedSteps >= totalSteps && totalSteps > 0)
                {
                    featureImpl.Status = FeatureImplementationStatus.Completed;
                }
                else if (completedSteps > 0)
                {
                    featureImpl.Status = FeatureImplementationStatus.InProgress;
                }
            }
        }

        // Update overall progress
        summary.CompletedTasks++;
        summary.OverallProgress = summary.TotalTasks > 0
            ? (summary.CompletedTasks * 100.0 / summary.TotalTasks)
            : 0;

        await SaveSummaryAsync(summary);
    }

    /// <summary>
    /// Updates area summaries based on current task states
    /// </summary>
    public async Task UpdateAreaSummariesAsync(
        string projectId,
        Dictionary<string, List<TaskRecord>> areaTasks)
    {
        var summary = await GetOrCreateSummaryAsync(projectId);

        summary.AreaSummaries.Clear();

        foreach (var kvp in areaTasks)
        {
            var areaName = kvp.Key;
            var tasks = kvp.Value;

            var areaSummary = new AreaSummary
            {
                AreaName = areaName,
                TotalTasks = tasks.Count,
                CompletedTasks = tasks.Count(t => t.Status == Models.Tasks.TaskStatus.Done),
                TaskIds = tasks.Select(t => t.Id).ToList(),
                FeatureIds = tasks
                    .Where(t => !string.IsNullOrEmpty(t.FeatureId))
                    .Select(t => t.FeatureId!)
                    .Distinct()
                    .ToList()
            };

            // Add files created/modified in this area
            foreach (var task in tasks)
            {
                if (summary.TaskToFeatureMap.TryGetValue(task.Id, out var featureId) &&
                    summary.FeatureImplementations.TryGetValue(featureId, out var featureImpl))
                {
                    foreach (var file in featureImpl.FilesCreated.Keys)
                    {
                        if (!areaSummary.FilesCreated.Contains(file))
                        {
                            areaSummary.FilesCreated.Add(file);
                        }
                    }
                }
            }

            summary.AreaSummaries.Add(areaSummary);
        }

        await SaveSummaryAsync(summary);
    }

    /// <summary>
    /// Gets implementation context for a task (specification summary + related files)
    /// </summary>
    public async Task<TaskImplementationContext> GetTaskContextAsync(
        string projectId,
        string taskId,
        string? featureId)
    {
        var summary = await GetOrCreateSummaryAsync(projectId);
        var context = new TaskImplementationContext();

        // Get specification version info
        context.SpecificationVersion = summary.SpecificationVersion;
        context.SpecificationContentHash = summary.SpecificationContentHash;

        // Get feature context if available
        if (!string.IsNullOrEmpty(featureId) &&
            summary.FeatureImplementations.TryGetValue(featureId, out var featureImpl))
        {
            context.FeatureId = featureId;
            context.FeatureName = featureImpl.FeatureName;
            context.FeatureStatus = featureImpl.Status;
            context.FeatureProgress = featureImpl.ProgressPercentage;

            // Get related files for this feature
            context.RelatedFiles = summary.GetFilesForFeature(featureId).ToList();
        }

        // Get task context
        var summaryFeatureId = summary.TaskToFeatureMap.GetValueOrDefault(taskId);
        if (!string.IsNullOrEmpty(summaryFeatureId) &&
            summary.FeatureImplementations.TryGetValue(summaryFeatureId, out var taskFeature))
        {
            context.PreviousStepsInTask = taskFeature.CompletedSteps;
        }

        return context;
    }

    /// <summary>
    /// Gets impact report showing which files implement which features
    /// </summary>
    public async Task<string> GenerateImpactReportAsync(string projectId)
    {
        var summary = await GetOrCreateSummaryAsync(projectId);
        var report = new System.Text.StringBuilder();

        report.AppendLine("# Implementation Impact Report");
        report.AppendLine();
        report.AppendLine($"**Project:** {summary.ProjectName}");
        report.AppendLine($"**Specification Version:** {summary.SpecificationVersion}");
        report.AppendLine($"**Overall Progress:** {summary.OverallProgress:F1}% ({summary.CompletedTasks}/{summary.TotalTasks} tasks)");
        report.AppendLine();

        // Feature breakdown
        report.AppendLine("## Feature Implementation Status");
        report.AppendLine();

        if (summary.FeatureImplementations.Any())
        {
            foreach (var kvp in summary.FeatureImplementations
                .OrderByDescending(f => f.Value.ProgressPercentage))
            {
                var feature = kvp.Value;
                var statusIcon = feature.Status switch
                {
                    FeatureImplementationStatus.Completed => "✅",
                    FeatureImplementationStatus.InProgress => "🔨",
                    FeatureImplementationStatus.NotStarted => "⏳",
                    FeatureImplementationStatus.Failed => "❌",
                    FeatureImplementationStatus.Blocked => "🚫",
                    _ => "❓"
                };

                report.AppendLine($"### {statusIcon} {feature.FeatureName ?? feature.FeatureId}");
                report.AppendLine();
                report.AppendLine($"**Progress:** {feature.ProgressPercentage:F1}% ({feature.CompletedSteps}/{feature.TotalSteps} steps)");
                report.AppendLine($"**Tasks:** {string.Join(", ", feature.TaskIds)}");
                report.AppendLine($"**Files Created:** {feature.FilesCreated.Count}");
                report.AppendLine($"**Files Modified:** {feature.FilesModified.Count}");
                report.AppendLine();

                if (feature.FilesCreated.Any())
                {
                    report.AppendLine("**Created Files:**");
                    foreach (var file in feature.FilesCreated.Keys.Take(10))
                    {
                        report.AppendLine($"  - `{file}`");
                    }
                    if (feature.FilesCreated.Count > 10)
                    {
                        report.AppendLine($"  - ... and {feature.FilesCreated.Count - 10} more");
                    }
                    report.AppendLine();
                }
            }
        }
        else
        {
            report.AppendLine("*No features tracked yet*");
            report.AppendLine();
        }

        // File impact matrix
        report.AppendLine("## File Impact Matrix");
        report.AppendLine();
        report.AppendLine("Shows which features each file implements:");
        report.AppendLine();

        if (summary.FileImpacts.Any())
        {
            foreach (var kvp in summary.FileImpacts
                .OrderByDescending(f => f.Value.RelatedFeatureIds.Count))
            {
                var impact = kvp.Value;
                var features = string.Join(", ", impact.RelatedFeatureIds);
                report.AppendLine($"- `{impact.FilePath}` → **{features}**");
                report.AppendLine($"  - Created by: {string.Join(", ", impact.CreatedByTasks.Keys)}");
                report.AppendLine($"  - Modified by: {string.Join(", ", impact.ModifiedByTasks.Keys)}");
            }
        }
        else
        {
            report.AppendLine("*No files tracked yet*");
        }

        return report.ToString();
    }

    /// <summary>
    /// Saves the summary to disk
    /// </summary>
    private async Task SaveSummaryAsync(ProjectImplementationSummary summary)
    {
        var project = _projectRepository.GetById(summary.ProjectId);
        if (project == null) return;

        var outputPath = GetProjectOutputPath(project);
        await summary.SaveAsync(outputPath);

        _logger?.LogDebug(
            "Saved implementation summary for project {ProjectId} ({TaskCount} tasks, {Progress:F0}% complete)",
            summary.ProjectId, summary.TotalTasks, summary.OverallProgress);
    }

    /// <summary>
    /// Gets the project output path
    /// </summary>
    private string GetProjectOutputPath(Project project)
    {
        var outputPath = project.Paths.Output;
        if (!Path.IsPathRooted(outputPath))
        {
            outputPath = Path.Combine(_projectsPath, outputPath);
        }
        return Path.GetFullPath(outputPath);
    }

    /// <summary>
    /// Loads a specification from disk
    /// </summary>
    private async Task<Specification?> LoadSpecificationAsync(string specPath, string projectId)
    {
        try
        {
            var content = await File.ReadAllTextAsync(specPath);
            return new Specification
            {
                Content = content,
                FilePath = specPath,
                ProjectId = projectId,
                ContentHash = Specification.ComputeHash(content)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load specification from {Path}", specPath);
            return null;
        }
    }

    /// <summary>
    /// Initializes summary from specification
    /// </summary>
    private void InitializeFromSpecification(ProjectImplementationSummary summary, Specification specification)
    {
        summary.UpdateSpecificationTracking(specification);
        summary.ProjectName = specification.Name;

        // Initialize feature implementations
        foreach (var feature in specification.Features)
        {
            if (!summary.FeatureImplementations.TryGetValue(feature.Id, out var featureImpl))
            {
                featureImpl = new FeatureImplementation
                {
                    FeatureId = feature.Id,
                    FeatureName = feature.Name,
                    Status = FeatureImplementationStatus.NotStarted
                };
                summary.FeatureImplementations[feature.Id] = featureImpl;
            }
            else
            {
                featureImpl.FeatureName = feature.Name;
            }

            // Initialize task tracking
            foreach (var taskId in feature.TaskIds)
            {
                if (!string.IsNullOrEmpty(taskId))
                {
                    if (!featureImpl.TaskIds.Contains(taskId)) { featureImpl.TaskIds.Add(taskId); }
                    summary.TaskToFeatureMap.TryAdd(taskId, feature.Id);
                }
            }
        }

        // Update total task count
        summary.TotalTasks = specification.Features
            .Sum(f => f.TaskIds.Count);
    }

    /// <summary>
    /// Persists all cached summaries
    /// </summary>
    public async Task PersistAllAsync()
    {
        var tasks = _summaries.Values.Select(summary => SaveSummaryAsync(summary));
        await Task.WhenAll(tasks);
    }
}

/// <summary>
/// Context provided to a Kobold when executing a task
/// </summary>
public class TaskImplementationContext
{
    /// <summary>
    /// Current specification version
    /// </summary>
    public int SpecificationVersion { get; set; }

    /// <summary>
    /// Current specification content hash
    /// </summary>
    public string SpecificationContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Feature ID this task implements (if known)
    /// </summary>
    public string? FeatureId { get; set; }

    /// <summary>
    /// Feature name this task implements
    /// </summary>
    public string? FeatureName { get; set; }

    /// <summary>
    /// Current status of the feature
    /// </summary>
    public FeatureImplementationStatus FeatureStatus { get; set; }

    /// <summary>
    /// Feature progress percentage
    /// </summary>
    public double FeatureProgress { get; set; }

    /// <summary>
    /// Files related to this feature (already created/modified)
    /// </summary>
    public List<string> RelatedFiles { get; set; } = new();

    /// <summary>
    /// Number of previous steps completed in this task
    /// </summary>
    public int PreviousStepsInTask { get; set; }

    /// <summary>
    /// Whether the specification has changed since task assignment
    /// </summary>
    public bool HasSpecificationChanged { get; set; }
}
