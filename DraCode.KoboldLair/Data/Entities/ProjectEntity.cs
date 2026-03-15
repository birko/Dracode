using Birko.Data.Models;
using Birko.Data.SQL.Attributes;
using Birko.Data.ViewModels;

namespace DraCode.KoboldLair.Data.Entities
{
    /// <summary>
    /// Database entity for Project. Flat structure suitable for SQL storage.
    /// Complex nested objects (Paths, Agents, Security, etc.) are stored as JSON text columns.
    /// </summary>
    [Table("projects")]
    public class ProjectEntity : AbstractDatabaseLogModel
    {
        // Core fields - frequently queried, stored as columns
        [RequiredField]
        [MaxLengthField(36)]
        public string ProjectId { get; set; } = "";

        [RequiredField]
        [MaxLengthField(255)]
        public string Name { get; set; } = "";

        [RequiredField]
        public int Status { get; set; } = 0;

        [RequiredField]
        public int ExecutionState { get; set; } = 0;

        public int VerificationStatus { get; set; } = 0;

        // Timestamps - stored as columns for query efficiency
        public DateTime? ProjectCreatedAt { get; set; }
        public DateTime? ProjectUpdatedAt { get; set; }
        public DateTime? AnalyzedAt { get; set; }
        public DateTime? LastProcessedAt { get; set; }
        public DateTime? VerificationStartedAt { get; set; }
        public DateTime? VerificationCompletedAt { get; set; }

        // Tracking - frequently accessed fields as columns
        [MaxLengthField(64)]
        public string? LastProcessedContentHash { get; set; }
        [MaxLengthField(36)]
        public string? SpecificationId { get; set; }
        [MaxLengthField(36)]
        public string? WyvernId { get; set; }
        public string? ErrorMessage { get; set; }

        // Complex objects stored as JSON text
        public string PathsJson { get; set; } = "{}";
        public string PendingAreasJson { get; set; } = "[]";
        public string AgentsJson { get; set; } = "{}";
        public string SecurityJson { get; set; } = "{}";
        public string VerificationReport { get; set; } = "";
        public string VerificationChecksJson { get; set; } = "[]";
        public string ExternalReferencesJson { get; set; } = "[]";
        public string MetadataJson { get; set; } = "{}";

        public override AbstractModel CopyTo(AbstractModel? clone = null)
        {
            var target = clone as ProjectEntity ?? new ProjectEntity();
            base.CopyTo(target);
            target.ProjectId = ProjectId;
            target.Name = Name;
            target.Status = Status;
            target.ExecutionState = ExecutionState;
            target.VerificationStatus = VerificationStatus;
            target.ProjectCreatedAt = ProjectCreatedAt;
            target.ProjectUpdatedAt = ProjectUpdatedAt;
            target.AnalyzedAt = AnalyzedAt;
            target.LastProcessedAt = LastProcessedAt;
            target.VerificationStartedAt = VerificationStartedAt;
            target.VerificationCompletedAt = VerificationCompletedAt;
            target.LastProcessedContentHash = LastProcessedContentHash;
            target.SpecificationId = SpecificationId;
            target.WyvernId = WyvernId;
            target.ErrorMessage = ErrorMessage;
            target.PathsJson = PathsJson;
            target.PendingAreasJson = PendingAreasJson;
            target.AgentsJson = AgentsJson;
            target.SecurityJson = SecurityJson;
            target.VerificationReport = VerificationReport;
            target.VerificationChecksJson = VerificationChecksJson;
            target.ExternalReferencesJson = ExternalReferencesJson;
            target.MetadataJson = MetadataJson;
            return target;
        }

        public override void LoadFrom(ModelViewModel data)
        {
            base.LoadFrom(data);
            if (data is ProjectViewModel vm)
            {
                ProjectId = vm.ProjectId;
                Name = vm.Name;
                Status = vm.Status;
                ExecutionState = vm.ExecutionState;
                VerificationStatus = vm.VerificationStatus;
                ProjectCreatedAt = vm.ProjectCreatedAt;
                ProjectUpdatedAt = vm.ProjectUpdatedAt;
                AnalyzedAt = vm.AnalyzedAt;
                LastProcessedAt = vm.LastProcessedAt;
                VerificationStartedAt = vm.VerificationStartedAt;
                VerificationCompletedAt = vm.VerificationCompletedAt;
                LastProcessedContentHash = vm.LastProcessedContentHash;
                SpecificationId = vm.SpecificationId;
                WyvernId = vm.WyvernId;
                ErrorMessage = vm.ErrorMessage;
                PathsJson = vm.PathsJson;
                PendingAreasJson = vm.PendingAreasJson;
                AgentsJson = vm.AgentsJson;
                SecurityJson = vm.SecurityJson;
                VerificationReport = vm.VerificationReport;
                VerificationChecksJson = vm.VerificationChecksJson;
                ExternalReferencesJson = vm.ExternalReferencesJson;
                MetadataJson = vm.MetadataJson;
            }
        }
    }
}
