using Birko.EventBus;

namespace DraCode.KoboldLair.Events
{
    /// <summary>
    /// Actions that can occur during a Kobold's lifecycle.
    /// </summary>
    public enum KoboldLifecycleAction
    {
        Started,
        Completed,
        Failed,
        TimedOut
    }

    /// <summary>
    /// Event published when a Kobold lifecycle transition occurs (started, completed, failed, timed out).
    /// </summary>
    public sealed record KoboldLifecycleEvent : EventBase
    {
        public required string ProjectId { get; init; }
        public required string TaskId { get; init; }
        public required string AgentType { get; init; }
        public required Guid KoboldId { get; init; }
        public required KoboldLifecycleAction Action { get; init; }

        public override string Source => "KoboldLair.Drake";
    }
}
