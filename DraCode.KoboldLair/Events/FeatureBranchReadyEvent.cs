using Birko.EventBus;

namespace DraCode.KoboldLair.Events
{
    /// <summary>
    /// Event published when a feature branch is ready for review/merge.
    /// </summary>
    public sealed record FeatureBranchReadyEvent : EventBase
    {
        public required string ProjectId { get; init; }
        public required string FeatureId { get; init; }
        public required string FeatureName { get; init; }
        public required string BranchName { get; init; }

        public override string Source => "KoboldLair.Wyvern";
    }
}
