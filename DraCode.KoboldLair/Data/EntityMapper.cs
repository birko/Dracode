using System.Text.Json;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Data
{
    /// <summary>
    /// Maps between domain models (Project, TaskRecord) and database entities (ProjectEntity, TaskEntity).
    /// Handles JSON serialization for complex nested objects.
    /// </summary>
    public static class EntityMapper
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        #region Project Mapping

        public static ProjectEntity ToEntity(Project project)
        {
            return new ProjectEntity
            {
                Guid = System.Guid.TryParse(project.Id, out var guid) ? guid : System.Guid.NewGuid(),
                ProjectId = project.Id,
                Name = project.Name,
                Status = (int)project.Status,
                ExecutionState = (int)project.ExecutionState,
                VerificationStatus = (int)project.VerificationStatus,
                ProjectCreatedAt = project.Timestamps.CreatedAt,
                ProjectUpdatedAt = project.Timestamps.UpdatedAt,
                AnalyzedAt = project.Timestamps.AnalyzedAt,
                LastProcessedAt = project.Timestamps.LastProcessedAt,
                VerificationStartedAt = project.VerificationStartedAt,
                VerificationCompletedAt = project.VerificationCompletedAt,
                LastProcessedContentHash = project.Tracking.LastProcessedContentHash,
                SpecificationId = project.Tracking.SpecificationId,
                WyvernId = project.Tracking.WyvernId,
                ErrorMessage = project.Tracking.ErrorMessage,
                PathsJson = JsonSerializer.Serialize(project.Paths, JsonOptions),
                PendingAreasJson = JsonSerializer.Serialize(project.Tracking.PendingAreas, JsonOptions),
                AgentsJson = JsonSerializer.Serialize(project.Agents, JsonOptions),
                SecurityJson = JsonSerializer.Serialize(project.Security, JsonOptions),
                VerificationReport = project.VerificationReport ?? "",
                VerificationChecksJson = JsonSerializer.Serialize(project.VerificationChecks, JsonOptions),
                ExternalReferencesJson = JsonSerializer.Serialize(project.ExternalProjectReferences, JsonOptions),
                MetadataJson = JsonSerializer.Serialize(project.Metadata, JsonOptions),
                CreatedAt = project.Timestamps.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = project.Timestamps.UpdatedAt ?? DateTime.UtcNow
            };
        }

        public static Project ToProject(ProjectEntity entity)
        {
            return new Project
            {
                Id = entity.ProjectId,
                Name = entity.Name,
                Status = (ProjectStatus)entity.Status,
                ExecutionState = (ProjectExecutionState)entity.ExecutionState,
                VerificationStatus = (VerificationStatus)entity.VerificationStatus,
                Timestamps = new ProjectTimestamps
                {
                    CreatedAt = entity.ProjectCreatedAt,
                    UpdatedAt = entity.ProjectUpdatedAt,
                    AnalyzedAt = entity.AnalyzedAt,
                    LastProcessedAt = entity.LastProcessedAt
                },
                VerificationStartedAt = entity.VerificationStartedAt,
                VerificationCompletedAt = entity.VerificationCompletedAt,
                Tracking = new ProjectTracking
                {
                    LastProcessedContentHash = entity.LastProcessedContentHash,
                    SpecificationId = entity.SpecificationId,
                    WyvernId = entity.WyvernId,
                    ErrorMessage = entity.ErrorMessage,
                    PendingAreas = DeserializeOrDefault(entity.PendingAreasJson, new List<string>())
                },
                Paths = DeserializeOrDefault(entity.PathsJson, new ProjectPaths()),
                Agents = DeserializeOrDefault(entity.AgentsJson, new AgentsConfig()),
                Security = DeserializeOrDefault(entity.SecurityJson, new SecurityConfig()),
                VerificationReport = string.IsNullOrEmpty(entity.VerificationReport) ? null : entity.VerificationReport,
                VerificationChecks = DeserializeOrDefault(entity.VerificationChecksJson, new List<VerificationCheck>()),
                ExternalProjectReferences = DeserializeOrDefault(entity.ExternalReferencesJson, new List<ExternalProjectReference>()),
                Metadata = DeserializeOrDefault(entity.MetadataJson, new Dictionary<string, string>())
            };
        }

        /// <summary>
        /// Updates an existing entity from a project, preserving the Guid.
        /// </summary>
        public static void UpdateEntity(ProjectEntity entity, Project project)
        {
            entity.ProjectId = project.Id;
            entity.Name = project.Name;
            entity.Status = (int)project.Status;
            entity.ExecutionState = (int)project.ExecutionState;
            entity.VerificationStatus = (int)project.VerificationStatus;
            entity.ProjectCreatedAt = project.Timestamps.CreatedAt;
            entity.ProjectUpdatedAt = project.Timestamps.UpdatedAt;
            entity.AnalyzedAt = project.Timestamps.AnalyzedAt;
            entity.LastProcessedAt = project.Timestamps.LastProcessedAt;
            entity.VerificationStartedAt = project.VerificationStartedAt;
            entity.VerificationCompletedAt = project.VerificationCompletedAt;
            entity.LastProcessedContentHash = project.Tracking.LastProcessedContentHash;
            entity.SpecificationId = project.Tracking.SpecificationId;
            entity.WyvernId = project.Tracking.WyvernId;
            entity.ErrorMessage = project.Tracking.ErrorMessage;
            entity.PathsJson = JsonSerializer.Serialize(project.Paths, JsonOptions);
            entity.PendingAreasJson = JsonSerializer.Serialize(project.Tracking.PendingAreas, JsonOptions);
            entity.AgentsJson = JsonSerializer.Serialize(project.Agents, JsonOptions);
            entity.SecurityJson = JsonSerializer.Serialize(project.Security, JsonOptions);
            entity.VerificationReport = project.VerificationReport ?? "";
            entity.VerificationChecksJson = JsonSerializer.Serialize(project.VerificationChecks, JsonOptions);
            entity.ExternalReferencesJson = JsonSerializer.Serialize(project.ExternalProjectReferences, JsonOptions);
            entity.MetadataJson = JsonSerializer.Serialize(project.Metadata, JsonOptions);
            entity.UpdatedAt = DateTime.UtcNow;
        }

        #endregion

        #region Task Mapping

        public static TaskEntity ToEntity(TaskRecord task, string? areaName = null)
        {
            return new TaskEntity
            {
                Guid = System.Guid.TryParse(task.Id, out var guid) ? guid : System.Guid.NewGuid(),
                TaskId = task.Id,
                TaskDescription = task.Task,
                AssignedAgent = task.AssignedAgent,
                ProjectId = task.ProjectId,
                AreaName = areaName,
                Status = (int)task.Status,
                Priority = (int)task.Priority,
                TaskCreatedAt = task.CreatedAt,
                TaskUpdatedAt = task.UpdatedAt,
                ErrorMessage = task.ErrorMessage,
                ErrorCategory = task.ErrorCategory,
                SpecificationVersion = task.SpecificationVersion,
                SpecificationContentHash = task.SpecificationContentHash,
                CommitSha = task.CommitSha,
                FeatureId = task.FeatureId,
                RetryCount = task.RetryCount,
                LastRetryAttempt = task.LastRetryAttempt,
                NextRetryAt = task.NextRetryAt,
                Provider = task.Provider,
                CommitFailed = task.CommitFailed,
                DependenciesJson = JsonSerializer.Serialize(task.Dependencies, JsonOptions),
                OutputFilesJson = JsonSerializer.Serialize(task.OutputFiles, JsonOptions),
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt ?? DateTime.UtcNow
            };
        }

        public static TaskRecord ToTaskRecord(TaskEntity entity)
        {
            return new TaskRecord
            {
                Id = entity.TaskId,
                Task = entity.TaskDescription,
                AssignedAgent = entity.AssignedAgent,
                ProjectId = entity.ProjectId,
                Status = (TaskStatus)entity.Status,
                Priority = (TaskPriority)entity.Priority,
                CreatedAt = entity.TaskCreatedAt,
                UpdatedAt = entity.TaskUpdatedAt,
                ErrorMessage = entity.ErrorMessage,
                ErrorCategory = entity.ErrorCategory,
                SpecificationVersion = entity.SpecificationVersion,
                SpecificationContentHash = entity.SpecificationContentHash,
                CommitSha = entity.CommitSha,
                FeatureId = entity.FeatureId,
                RetryCount = entity.RetryCount,
                LastRetryAttempt = entity.LastRetryAttempt,
                NextRetryAt = entity.NextRetryAt,
                Provider = entity.Provider,
                CommitFailed = entity.CommitFailed,
                Dependencies = DeserializeOrDefault(entity.DependenciesJson, new List<string>()),
                OutputFiles = DeserializeOrDefault(entity.OutputFilesJson, new List<string>())
            };
        }

        /// <summary>
        /// Updates an existing entity from a task record, preserving the Guid.
        /// </summary>
        public static void UpdateEntity(TaskEntity entity, TaskRecord task, string? areaName = null)
        {
            entity.TaskId = task.Id;
            entity.TaskDescription = task.Task;
            entity.AssignedAgent = task.AssignedAgent;
            entity.ProjectId = task.ProjectId;
            if (areaName != null) entity.AreaName = areaName;
            entity.Status = (int)task.Status;
            entity.Priority = (int)task.Priority;
            entity.TaskCreatedAt = task.CreatedAt;
            entity.TaskUpdatedAt = task.UpdatedAt;
            entity.ErrorMessage = task.ErrorMessage;
            entity.ErrorCategory = task.ErrorCategory;
            entity.SpecificationVersion = task.SpecificationVersion;
            entity.SpecificationContentHash = task.SpecificationContentHash;
            entity.CommitSha = task.CommitSha;
            entity.FeatureId = task.FeatureId;
            entity.RetryCount = task.RetryCount;
            entity.LastRetryAttempt = task.LastRetryAttempt;
            entity.NextRetryAt = task.NextRetryAt;
            entity.Provider = task.Provider;
            entity.CommitFailed = task.CommitFailed;
            entity.DependenciesJson = JsonSerializer.Serialize(task.Dependencies, JsonOptions);
            entity.OutputFilesJson = JsonSerializer.Serialize(task.OutputFiles, JsonOptions);
            entity.UpdatedAt = DateTime.UtcNow;
        }

        #endregion

        #region Helpers

        private static T DeserializeOrDefault<T>(string? json, T defaultValue) where T : class
        {
            if (string.IsNullOrEmpty(json))
                return defaultValue;
            try
            {
                return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        #endregion
    }
}
