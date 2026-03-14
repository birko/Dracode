using System.Text.Json;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Project-level implementation summary that ties specifications to tasks and files.
    /// Provides cross-task context and impact tracking.
    /// </summary>
    public class ProjectImplementationSummary
    {
        /// <summary>
        /// Project identifier
        /// </summary>
        public string ProjectId { get; set; } = string.Empty;

        /// <summary>
        /// Project name
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// When this summary was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When this summary was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Specification version this summary is based on
        /// </summary>
        public int SpecificationVersion { get; set; } = 1;

        /// <summary>
        /// Specification content hash for change detection
        /// </summary>
        public string SpecificationContentHash { get; set; } = string.Empty;

        /// <summary>
        /// Maps feature IDs to their implementation details
        /// </summary>
        public Dictionary<string, FeatureImplementation> FeatureImplementations { get; set; } = new();

        /// <summary>
        /// All files touched by implementation, keyed by path
        /// </summary>
        public Dictionary<string, FileImpact> FileImpacts { get; set; } = new();

        /// <summary>
        /// Task-to-feature mapping (many tasks can implement one feature)
        /// </summary>
        public Dictionary<string, string> TaskToFeatureMap { get; set; } = new();

        /// <summary>
        /// Area summaries for high-level progress tracking
        /// </summary>
        public List<AreaSummary> AreaSummaries { get; set; } = new();

        /// <summary>
        /// Overall implementation progress percentage (0-100)
        /// </summary>
        public double OverallProgress { get; set; }

        /// <summary>
        /// Total number of tasks across all features
        /// </summary>
        public int TotalTasks { get; set; }

        /// <summary>
        /// Number of completed tasks
        /// </summary>
        public int CompletedTasks { get; set; }

        /// <summary>
        /// Gets the file path for this summary
        /// </summary>
        public static string GetFilePath(string projectOutputPath)
        {
            return Path.Combine(projectOutputPath, "implementation-summary.json");
        }

        /// <summary>
        /// Saves the summary to disk
        /// </summary>
        public async Task SaveAsync(string projectOutputPath)
        {
            UpdatedAt = DateTime.UtcNow;
            var filePath = GetFilePath(projectOutputPath);
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(this, options);
            await File.WriteAllTextAsync(filePath, json);
        }

        /// <summary>
        /// Loads a summary from disk
        /// </summary>
        public static async Task<ProjectImplementationSummary?> LoadAsync(string projectOutputPath)
        {
            var filePath = GetFilePath(projectOutputPath);
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<ProjectImplementationSummary>(json, options);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Updates the summary with completed step information
        /// </summary>
        public void RecordStepCompletion(string taskId, string featureId, ImplementationStep step)
        {
            // Update task-to-feature mapping
            if (!string.IsNullOrEmpty(featureId))
            {
                TaskToFeatureMap[taskId] = featureId;
            }

            // Update feature implementation
            if (!string.IsNullOrEmpty(featureId))
            {
                if (!FeatureImplementations.TryGetValue(featureId, out var featureImpl))
                {
                    featureImpl = new FeatureImplementation { FeatureId = featureId };
                    FeatureImplementations[featureId] = featureImpl;
                }

                featureImpl.CompletedSteps++;
                featureImpl.LastActivityAt = DateTime.UtcNow;

                // Track files for this feature
                foreach (var file in step.FilesToCreate)
                {
                    featureImpl.FilesCreated.TryAdd(file, DateTime.UtcNow);
                    RecordFileImpact(file, taskId, featureId, step, isCreation: true);
                }

                foreach (var file in step.FilesToModify)
                {
                    featureImpl.FilesModified.TryAdd(file, DateTime.UtcNow);
                    RecordFileImpact(file, taskId, featureId, step, isCreation: false);
                }
            }
        }

        /// <summary>
        /// Records the impact of a file being created or modified
        /// </summary>
        private void RecordFileImpact(string filePath, string taskId, string featureId, ImplementationStep step, bool isCreation)
        {
            if (!FileImpacts.TryGetValue(filePath, out var impact))
            {
                impact = new FileImpact { FilePath = filePath };
                FileImpacts[filePath] = impact;
            }

            impact.LastModifiedAt = DateTime.UtcNow;

            if (isCreation)
            {
                impact.CreatedByTasks.TryAdd(taskId, step.Title);
            }
            else
            {
                impact.ModifiedByTasks.TryAdd(taskId, step.Title);
            }

            if (!string.IsNullOrEmpty(featureId))
            {
                impact.RelatedFeatureIds.Add(featureId);
            }
        }

        /// <summary>
        /// Gets all files that implement a specific feature
        /// </summary>
        public HashSet<string> GetFilesForFeature(string featureId)
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (FeatureImplementations.TryGetValue(featureId, out var featureImpl))
            {
                files.UnionWith(featureImpl.FilesCreated.Keys);
                files.UnionWith(featureImpl.FilesModified.Keys);
            }

            return files;
        }

        /// <summary>
        /// Gets which features are implemented by a specific file
        /// </summary>
        public List<string> GetFeaturesForFile(string filePath)
        {
            if (FileImpacts.TryGetValue(filePath, out var impact))
            {
                return impact.RelatedFeatureIds.ToList();
            }
            return new List<string>();
        }

        /// <summary>
        /// Checks if specification has changed since last summary update
        /// </summary>
        public bool HasSpecificationChanged(Specification specification)
        {
            return SpecificationContentHash != specification.ContentHash ||
                   SpecificationVersion != specification.Version;
        }

        /// <summary>
        /// Updates specification tracking
        /// </summary>
        public void UpdateSpecificationTracking(Specification specification)
        {
            SpecificationVersion = specification.Version;
            SpecificationContentHash = specification.ContentHash;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Implementation details for a single feature
    /// </summary>
    public class FeatureImplementation
    {
        /// <summary>
        /// Feature ID from specification
        /// </summary>
        public string FeatureId { get; set; } = string.Empty;

        /// <summary>
        /// Feature name
        /// </summary>
        public string FeatureName { get; set; } = string.Empty;

        /// <summary>
        /// Task IDs that implement this feature
        /// </summary>
        public List<string> TaskIds { get; set; } = new();

        /// <summary>
        /// Total steps across all tasks for this feature
        /// </summary>
        public int TotalSteps { get; set; }

        /// <summary>
        /// Number of completed steps
        /// </summary>
        public int CompletedSteps { get; set; }

        /// <summary>
        /// Files created for this feature (path -> timestamp)
        /// </summary>
        public Dictionary<string, DateTime> FilesCreated { get; set; } = new();

        /// <summary>
        /// Files modified for this feature (path -> timestamp)
        /// </summary>
        public Dictionary<string, DateTime> FilesModified { get; set; } = new();

        /// <summary>
        /// When this feature was last worked on
        /// </summary>
        public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Feature implementation status
        /// </summary>
        public FeatureImplementationStatus Status { get; set; } = FeatureImplementationStatus.NotStarted;

        /// <summary>
        /// Gets progress percentage for this feature
        /// </summary>
        public double ProgressPercentage => TotalSteps > 0 ? (CompletedSteps * 100.0 / TotalSteps) : 0;
    }

    /// <summary>
    /// Impact tracking for a single file
    /// </summary>
    public class FileImpact
    {
        /// <summary>
        /// Relative path to the file
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Tasks that created this file (taskId -> step title)
        /// </summary>
        public Dictionary<string, string> CreatedByTasks { get; set; } = new();

        /// <summary>
        /// Tasks that modified this file (taskId -> step title)
        /// </summary>
        public Dictionary<string, string> ModifiedByTasks { get; set; } = new();

        /// <summary>
        /// Feature IDs that this file implements
        /// </summary>
        public HashSet<string> RelatedFeatureIds { get; set; } = new();

        /// <summary>
        /// When this file was last touched
        /// </summary>
        public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// File category inferred from extension
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Purpose/description of what this file does
        /// </summary>
        public string Purpose { get; set; } = string.Empty;
    }

    /// <summary>
    /// Summary of an area's implementation status
    /// </summary>
    public class AreaSummary
    {
        /// <summary>
        /// Area name (e.g., "Frontend", "Backend")
        /// </summary>
        public string AreaName { get; set; } = string.Empty;

        /// <summary>
        /// Number of tasks in this area
        /// </summary>
        public int TotalTasks { get; set; }

        /// <summary>
        /// Number of completed tasks
        /// </summary>
        public int CompletedTasks { get; set; }

        /// <summary>
        /// Task IDs in this area
        /// </summary>
        public List<string> TaskIds { get; set; } = new();

        /// <summary>
        /// Feature IDs being implemented in this area
        /// </summary>
        public List<string> FeatureIds { get; set; } = new();

        /// <summary>
        /// Files created in this area
        /// </summary>
        public List<string> FilesCreated { get; set; } = new();

        /// <summary>
        /// Progress percentage
        /// </summary>
        public double ProgressPercentage => TotalTasks > 0 ? (CompletedTasks * 100.0 / TotalTasks) : 0;
    }

    /// <summary>
    /// Status of feature implementation
    /// </summary>
    public enum FeatureImplementationStatus
    {
        /// <summary>
        /// No work has started
        /// </summary>
        NotStarted,

        /// <summary>
        /// Work is in progress
        /// </summary>
        InProgress,

        /// <summary>
        /// Feature is complete
        /// </summary>
        Completed,

        /// <summary>
        /// Feature implementation failed
        /// </summary>
        Failed,

        /// <summary>
        /// Feature was blocked by dependencies
        /// </summary>
        Blocked
    }
}
