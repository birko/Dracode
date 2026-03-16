using Birko.Data.Models;
using Birko.Data.SQL.Attributes;
using Birko.Data.ViewModels;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for TaskRecord. Flat structure suitable for SQL storage.
    /// List fields (Dependencies, OutputFiles) stored as JSON text columns.
    /// </summary>
    [Table("tasks")]
    public class TaskEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(36)]
        public string TaskId { get; set; } = "";

        [RequiredField]
        public string TaskDescription { get; set; } = "";

        [RequiredField]
        [MaxLengthField(50)]
        public string AssignedAgent { get; set; } = "";

        [MaxLengthField(36)]
        public string? ProjectId { get; set; }

        [MaxLengthField(50)]
        public string? AreaName { get; set; }

        [RequiredField]
        public int Status { get; set; } = 0;

        [RequiredField]
        public int Priority { get; set; } = 1;

        public DateTime TaskCreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? TaskUpdatedAt { get; set; }

        public string? ErrorMessage { get; set; }
        [MaxLengthField(20)]
        public string? ErrorCategory { get; set; }

        public int SpecificationVersion { get; set; } = 1;
        [MaxLengthField(64)]
        public string? SpecificationContentHash { get; set; }

        [MaxLengthField(40)]
        public string? CommitSha { get; set; }
        [MaxLengthField(36)]
        public string? FeatureId { get; set; }

        // Retry tracking
        public int RetryCount { get; set; } = 0;
        public DateTime? LastRetryAttempt { get; set; }
        public DateTime? NextRetryAt { get; set; }

        [MaxLengthField(50)]
        public string? Provider { get; set; }

        public bool CommitFailed { get; set; }

        // List fields stored as JSON
        public string DependenciesJson { get; set; } = "[]";
        public string OutputFilesJson { get; set; } = "[]";

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as TaskEntity ?? new TaskEntity();
            base.CopyTo(target);
            target.TaskId = TaskId;
            target.TaskDescription = TaskDescription;
            target.AssignedAgent = AssignedAgent;
            target.ProjectId = ProjectId;
            target.AreaName = AreaName;
            target.Status = Status;
            target.Priority = Priority;
            target.TaskCreatedAt = TaskCreatedAt;
            target.TaskUpdatedAt = TaskUpdatedAt;
            target.ErrorMessage = ErrorMessage;
            target.ErrorCategory = ErrorCategory;
            target.SpecificationVersion = SpecificationVersion;
            target.SpecificationContentHash = SpecificationContentHash;
            target.CommitSha = CommitSha;
            target.FeatureId = FeatureId;
            target.RetryCount = RetryCount;
            target.LastRetryAttempt = LastRetryAttempt;
            target.NextRetryAt = NextRetryAt;
            target.Provider = Provider;
            target.CommitFailed = CommitFailed;
            target.DependenciesJson = DependenciesJson;
            target.OutputFilesJson = OutputFilesJson;
            return target;
        }

        public override void LoadFrom(ModelViewModel data)
        {
            base.LoadFrom(data);
            if (data is TaskViewModel vm)
            {
                TaskId = vm.TaskId;
                TaskDescription = vm.TaskDescription;
                AssignedAgent = vm.AssignedAgent;
                ProjectId = vm.ProjectId;
                AreaName = vm.AreaName;
                Status = vm.Status;
                Priority = vm.Priority;
                TaskCreatedAt = vm.TaskCreatedAt;
                TaskUpdatedAt = vm.TaskUpdatedAt;
                ErrorMessage = vm.ErrorMessage;
                ErrorCategory = vm.ErrorCategory;
                SpecificationVersion = vm.SpecificationVersion;
                SpecificationContentHash = vm.SpecificationContentHash;
                CommitSha = vm.CommitSha;
                FeatureId = vm.FeatureId;
                RetryCount = vm.RetryCount;
                LastRetryAttempt = vm.LastRetryAttempt;
                NextRetryAt = vm.NextRetryAt;
                Provider = vm.Provider;
                CommitFailed = vm.CommitFailed;
                DependenciesJson = vm.DependenciesJson;
                OutputFilesJson = vm.OutputFilesJson;
            }
        }
    }
}
