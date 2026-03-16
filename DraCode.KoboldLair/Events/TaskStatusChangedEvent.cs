using Birko.EventBus;

namespace DraCode.KoboldLair.Events
{
    /// <summary>
    /// Event published when a task's status changes (e.g., Working -> Done, Working -> Failed).
    /// </summary>
    public sealed record TaskStatusChangedEvent : EventBase
    {
        public required string ProjectId { get; init; }
        public required string TaskId { get; init; }
        public required string OldStatus { get; init; }
        public required string NewStatus { get; init; }
        public string? ErrorMessage { get; init; }

        public override string Source => "KoboldLair.Drake";
    }
}
