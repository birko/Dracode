using Birko.AI.Orchestration.Models;

namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// A Kobold task's implementation plan: Birko's <see cref="ImplementationPlan"/> plus the KoboldLair context it is
    /// stored and resumed with (TASK-126). Plans written before the move to Birko load through
    /// <see cref="Services.LegacyPlanJson.Upgrade"/>.
    /// </summary>
    public class KoboldImplementationPlan : ImplementationPlan
    {
        /// <summary>
        /// Human-readable filename for this plan (without extension), generated from the task description with a
        /// uniqueness suffix. Example: "frontend-1-user-authentication-a7f3".
        /// </summary>
        public string? PlanFilename { get; set; }

        /// <summary>Name of the specification feature this task implements.</summary>
        public string? FeatureName { get; set; }

        /// <summary>Description of that feature, from the specification.</summary>
        public string? FeatureDescription { get; set; }

        /// <summary>Specification version when this plan was created — detects specification drift during execution.</summary>
        public int SpecificationVersion { get; set; } = 1;

        /// <summary>Specification content hash when this plan was created.</summary>
        public string SpecificationContentHash { get; set; } = string.Empty;

        /// <summary>The parts of the specification relevant to this task, condensed to its scope.</summary>
        public string? SpecificationContext { get; set; }

        /// <summary>
        /// Set on a parallel batch Kobold's copy of the plan (0-based step positions): the steps this copy owns. Saving
        /// such a copy merges those steps and its new log entries into the stored plan rather than writing the copy over
        /// it (TASK-171, FEATURE-028 D10). Never stored.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public IReadOnlyList<int>? BatchSteps { get; set; }
    }
}
