using Birko.Data.Models;
using Birko.Data.SQL.Attributes;

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
        }
    }
}
