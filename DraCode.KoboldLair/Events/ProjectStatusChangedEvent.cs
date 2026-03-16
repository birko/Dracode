using Birko.EventBus;

namespace DraCode.KoboldLair.Events
{
    /// <summary>
    /// Event published when a project's overall status changes.
    /// </summary>
    public sealed record ProjectStatusChangedEvent : EventBase
    {
        public required string ProjectId { get; init; }
        public required string ProjectName { get; init; }
        public required string OldStatus { get; init; }
        public required string NewStatus { get; init; }

        public override string Source => "KoboldLair.ProjectService";
    }
}
