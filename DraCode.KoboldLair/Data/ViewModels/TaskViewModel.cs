using Birko.Data.Models;
using Birko.Data.ViewModels;
using DraCode.KoboldLair.Data.Entities;

namespace DraCode.KoboldLair.Data
{
    /// <summary>
    /// ViewModel for TaskEntity. Implements bidirectional data transfer
    /// between the UI/service layer and the database entity.
    /// </summary>
    public class TaskViewModel : LogViewModel, ILoadable<TaskEntity>, ILoadable<TaskViewModel>
    {
        public string TaskId { get; set; } = "";
        public string TaskDescription { get; set; } = "";
        public string AssignedAgent { get; set; } = "";
        public string? ProjectId { get; set; }
        public string? AreaName { get; set; }
        public int Status { get; set; } = 0;
        public int Priority { get; set; } = 1;
        public DateTime TaskCreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? TaskUpdatedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCategory { get; set; }
        public int SpecificationVersion { get; set; } = 1;
        public string? SpecificationContentHash { get; set; }
        public string? CommitSha { get; set; }
        public string? FeatureId { get; set; }
        public int RetryCount { get; set; } = 0;
        public DateTime? LastRetryAttempt { get; set; }
        public DateTime? NextRetryAt { get; set; }
        public string? Provider { get; set; }
        public string DependenciesJson { get; set; } = "[]";
        public string OutputFilesJson { get; set; } = "[]";

        public void LoadFrom(TaskEntity data)
        {
            base.LoadFrom((AbstractModel)data);
            if (data != null)
            {
                TaskId = data.TaskId;
                TaskDescription = data.TaskDescription;
                AssignedAgent = data.AssignedAgent;
                ProjectId = data.ProjectId;
                AreaName = data.AreaName;
                Status = data.Status;
                Priority = data.Priority;
                TaskCreatedAt = data.TaskCreatedAt;
                TaskUpdatedAt = data.TaskUpdatedAt;
                ErrorMessage = data.ErrorMessage;
                ErrorCategory = data.ErrorCategory;
                SpecificationVersion = data.SpecificationVersion;
                SpecificationContentHash = data.SpecificationContentHash;
                CommitSha = data.CommitSha;
                FeatureId = data.FeatureId;
                RetryCount = data.RetryCount;
                LastRetryAttempt = data.LastRetryAttempt;
                NextRetryAt = data.NextRetryAt;
                Provider = data.Provider;
                DependenciesJson = data.DependenciesJson;
                OutputFilesJson = data.OutputFilesJson;
            }
        }

        public void LoadFrom(TaskViewModel data)
        {
            base.LoadFrom((LogViewModel)data);
            if (data != null)
            {
                TaskId = data.TaskId;
                TaskDescription = data.TaskDescription;
                AssignedAgent = data.AssignedAgent;
                ProjectId = data.ProjectId;
                AreaName = data.AreaName;
                Status = data.Status;
                Priority = data.Priority;
                TaskCreatedAt = data.TaskCreatedAt;
                TaskUpdatedAt = data.TaskUpdatedAt;
                ErrorMessage = data.ErrorMessage;
                ErrorCategory = data.ErrorCategory;
                SpecificationVersion = data.SpecificationVersion;
                SpecificationContentHash = data.SpecificationContentHash;
                CommitSha = data.CommitSha;
                FeatureId = data.FeatureId;
                RetryCount = data.RetryCount;
                LastRetryAttempt = data.LastRetryAttempt;
                NextRetryAt = data.NextRetryAt;
                Provider = data.Provider;
                DependenciesJson = data.DependenciesJson;
                OutputFilesJson = data.OutputFilesJson;
            }
        }
    }
}
