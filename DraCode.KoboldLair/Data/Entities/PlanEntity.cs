using Birko.Data.Models;
using Birko.Data.SQL.Attributes;
using Birko.Data.ViewModels;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for KoboldImplementationPlan.
    /// Core fields as columns, complex data (steps, reflections, escalations) as JSON.
    /// </summary>
    [Table("plans")]
    public class PlanEntity : AbstractDatabaseLogModel
    {
        [RequiredField]
        [MaxLengthField(36)]
        public string TaskId { get; set; } = "";

        [RequiredField]
        [MaxLengthField(36)]
        public string ProjectId { get; set; } = "";

        [MaxLengthField(100)]
        public string? PlanFilename { get; set; }

        [MaxLengthField(500)]
        public string TaskDescription { get; set; } = "";

        /// <summary>
        /// 0=Planning, 1=Ready, 2=InProgress, 3=Completed, 4=Failed
        /// </summary>
        public int Status { get; set; } = 0;

        public int CurrentStepIndex { get; set; } = 0;

        public string? ErrorMessage { get; set; }

        public int SpecificationVersion { get; set; } = 1;

        [MaxLengthField(64)]
        public string? SpecificationContentHash { get; set; }

        [MaxLengthField(36)]
        public string? FeatureId { get; set; }

        [MaxLengthField(200)]
        public string? FeatureName { get; set; }

        public DateTime PlanCreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime PlanUpdatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Full plan data serialized as JSON (steps, reflections, escalations, etc.)
        /// </summary>
        public string PlanDataJson { get; set; } = "{}";

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as PlanEntity ?? new PlanEntity();
            base.CopyTo(target);
            target.TaskId = TaskId;
            target.ProjectId = ProjectId;
            target.PlanFilename = PlanFilename;
            target.TaskDescription = TaskDescription;
            target.Status = Status;
            target.CurrentStepIndex = CurrentStepIndex;
            target.ErrorMessage = ErrorMessage;
            target.SpecificationVersion = SpecificationVersion;
            target.SpecificationContentHash = SpecificationContentHash;
            target.FeatureId = FeatureId;
            target.FeatureName = FeatureName;
            target.PlanCreatedAt = PlanCreatedAt;
            target.PlanUpdatedAt = PlanUpdatedAt;
            target.PlanDataJson = PlanDataJson;
            return target;
        }

        public override void LoadFrom(IGuidEntity data)
        {
            base.LoadFrom(data);
            if (data is PlanViewModel vm)
            {
                TaskId = vm.TaskId;
                ProjectId = vm.ProjectId;
                PlanFilename = vm.PlanFilename;
                TaskDescription = vm.TaskDescription;
                Status = vm.Status;
                CurrentStepIndex = vm.CurrentStepIndex;
                ErrorMessage = vm.ErrorMessage;
                SpecificationVersion = vm.SpecificationVersion;
                SpecificationContentHash = vm.SpecificationContentHash;
                FeatureId = vm.FeatureId;
                FeatureName = vm.FeatureName;
                PlanCreatedAt = vm.PlanCreatedAt;
                PlanUpdatedAt = vm.PlanUpdatedAt;
                PlanDataJson = vm.PlanDataJson;
            }
        }
    }

    public class PlanViewModel : LogViewModel
    {
        public string TaskId { get; set; } = "";
        public string ProjectId { get; set; } = "";
        public string? PlanFilename { get; set; }
        public string TaskDescription { get; set; } = "";
        public int Status { get; set; } = 0;
        public int CurrentStepIndex { get; set; } = 0;
        public string? ErrorMessage { get; set; }
        public int SpecificationVersion { get; set; } = 1;
        public string? SpecificationContentHash { get; set; }
        public string? FeatureId { get; set; }
        public string? FeatureName { get; set; }
        public DateTime PlanCreatedAt { get; set; }
        public DateTime PlanUpdatedAt { get; set; }
        public string PlanDataJson { get; set; } = "{}";

        public void LoadFrom(PlanEntity data)
        {
            base.LoadFrom((AbstractModel)data);
            if (data != null)
            {
                TaskId = data.TaskId;
                ProjectId = data.ProjectId;
                PlanFilename = data.PlanFilename;
                TaskDescription = data.TaskDescription;
                Status = data.Status;
                CurrentStepIndex = data.CurrentStepIndex;
                ErrorMessage = data.ErrorMessage;
                SpecificationVersion = data.SpecificationVersion;
                SpecificationContentHash = data.SpecificationContentHash;
                FeatureId = data.FeatureId;
                FeatureName = data.FeatureName;
                PlanCreatedAt = data.PlanCreatedAt;
                PlanUpdatedAt = data.PlanUpdatedAt;
                PlanDataJson = data.PlanDataJson;
            }
        }
    }
}
