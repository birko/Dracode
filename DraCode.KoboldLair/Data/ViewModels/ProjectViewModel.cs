using Birko.Data.Models;
using Birko.Data.ViewModels;
using DraCode.KoboldLair.Data.Entities;

namespace DraCode.KoboldLair.Data
{
    /// <summary>
    /// ViewModel for ProjectEntity. Implements bidirectional data transfer
    /// between the UI/service layer and the database entity.
    /// </summary>
    public class ProjectViewModel : LogViewModel, ILoadable<ProjectEntity>, ILoadable<ProjectViewModel>
    {
        public string ProjectId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Status { get; set; } = 0;
        public int ExecutionState { get; set; } = 0;
        public int VerificationStatus { get; set; } = 0;

        public DateTime? ProjectCreatedAt { get; set; }
        public DateTime? ProjectUpdatedAt { get; set; }
        public DateTime? AnalyzedAt { get; set; }
        public DateTime? LastProcessedAt { get; set; }
        public DateTime? VerificationStartedAt { get; set; }
        public DateTime? VerificationCompletedAt { get; set; }

        public string? LastProcessedContentHash { get; set; }
        public string? SpecificationId { get; set; }
        public string? WyvernId { get; set; }
        public string? ErrorMessage { get; set; }

        public string PathsJson { get; set; } = "{}";
        public string PendingAreasJson { get; set; } = "[]";
        public string AgentsJson { get; set; } = "{}";
        public string SecurityJson { get; set; } = "{}";
        public string VerificationReport { get; set; } = "";
        public string VerificationChecksJson { get; set; } = "[]";
        public string ExternalReferencesJson { get; set; } = "[]";
        public string MetadataJson { get; set; } = "{}";

        public void LoadFrom(ProjectEntity data)
        {
            base.LoadFrom((AbstractModel)data);
            if (data != null)
            {
                ProjectId = data.ProjectId;
                Name = data.Name;
                Status = data.Status;
                ExecutionState = data.ExecutionState;
                VerificationStatus = data.VerificationStatus;
                ProjectCreatedAt = data.ProjectCreatedAt;
                ProjectUpdatedAt = data.ProjectUpdatedAt;
                AnalyzedAt = data.AnalyzedAt;
                LastProcessedAt = data.LastProcessedAt;
                VerificationStartedAt = data.VerificationStartedAt;
                VerificationCompletedAt = data.VerificationCompletedAt;
                LastProcessedContentHash = data.LastProcessedContentHash;
                SpecificationId = data.SpecificationId;
                WyvernId = data.WyvernId;
                ErrorMessage = data.ErrorMessage;
                PathsJson = data.PathsJson;
                PendingAreasJson = data.PendingAreasJson;
                AgentsJson = data.AgentsJson;
                SecurityJson = data.SecurityJson;
                VerificationReport = data.VerificationReport;
                VerificationChecksJson = data.VerificationChecksJson;
                ExternalReferencesJson = data.ExternalReferencesJson;
                MetadataJson = data.MetadataJson;
            }
        }

        public void LoadFrom(ProjectViewModel data)
        {
            base.LoadFrom((LogViewModel)data);
            if (data != null)
            {
                ProjectId = data.ProjectId;
                Name = data.Name;
                Status = data.Status;
                ExecutionState = data.ExecutionState;
                VerificationStatus = data.VerificationStatus;
                ProjectCreatedAt = data.ProjectCreatedAt;
                ProjectUpdatedAt = data.ProjectUpdatedAt;
                AnalyzedAt = data.AnalyzedAt;
                LastProcessedAt = data.LastProcessedAt;
                VerificationStartedAt = data.VerificationStartedAt;
                VerificationCompletedAt = data.VerificationCompletedAt;
                LastProcessedContentHash = data.LastProcessedContentHash;
                SpecificationId = data.SpecificationId;
                WyvernId = data.WyvernId;
                ErrorMessage = data.ErrorMessage;
                PathsJson = data.PathsJson;
                PendingAreasJson = data.PendingAreasJson;
                AgentsJson = data.AgentsJson;
                SecurityJson = data.SecurityJson;
                VerificationReport = data.VerificationReport;
                VerificationChecksJson = data.VerificationChecksJson;
                ExternalReferencesJson = data.ExternalReferencesJson;
                MetadataJson = data.MetadataJson;
            }
        }
    }
}
