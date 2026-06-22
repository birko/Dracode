using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Events.Run
{
    /// <summary>
    /// Base of the per-run telemetry stream a Kobold publishes during execution (TASK-037).
    /// Deliberately a plain record — NOT a <c>Birko.EventBus</c> event — so it rides the dedicated,
    /// non-blocking <see cref="Services.KoboldRunEventSource"/> rather than the awaited domain bus.
    /// The WS (TASK-038) and SSE (TASK-045) transports subscribe to this.
    /// </summary>
    public abstract record KoboldRunEvent
    {
        /// <summary>Identifies one Kobold execution (a fresh Guid per StartWorking call; survives across retries as distinct runs).</summary>
        public Guid RunId { get; init; }
        public Guid KoboldId { get; init; }
        public string? ProjectId { get; init; }
        public string? TaskId { get; init; }
        public string AgentType { get; init; } = "";
        public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
        /// <summary>Monotonic per-run ordinal assigned by the event source, so subscribers can order/dedup.</summary>
        public int Sequence { get; init; }

        /// <summary>Discriminator for transports serializing to a flat wire format.</summary>
        public abstract string Kind { get; }
    }

    /// <summary>A tool invocation began. <paramref name="InputJson"/> is best-effort (may be a preview).</summary>
    public sealed record ToolCallStartedEvent : KoboldRunEvent
    {
        public string ToolName { get; init; } = "";
        public string InputJson { get; init; } = "";
        public override string Kind => "tool_call_started";
    }

    /// <summary>A tool invocation returned. <paramref name="ResultPreview"/> may be truncated.</summary>
    public sealed record ToolCallResultEvent : KoboldRunEvent
    {
        public string ToolName { get; init; } = "";
        public string ResultPreview { get; init; } = "";
        public override string Kind => "tool_call_result";
    }

    /// <summary>A self-reflection checkpoint (reuses the existing <see cref="ReflectionEntry"/> / <see cref="EscalationAlert"/>).</summary>
    public sealed record ReflectionEvent : KoboldRunEvent
    {
        public ReflectionEntry Entry { get; init; } = new();
        public EscalationAlert? Escalation { get; init; }
        public override string Kind => "reflection";
    }

    /// <summary>A plan step changed status (reuses the existing <see cref="StepStatus"/>).</summary>
    public sealed record PlanStepUpdatedEvent : KoboldRunEvent
    {
        public int StepIndex { get; init; }
        public StepStatus Status { get; init; }
        public string? Output { get; init; }
        public int CompletedSteps { get; init; }
        public int TotalSteps { get; init; }
        public override string Kind => "plan_step_updated";
    }

    /// <summary>The run finished (any terminal <see cref="KoboldStatus"/>).</summary>
    public sealed record RunCompletedEvent : KoboldRunEvent
    {
        public KoboldStatus FinalStatus { get; init; }
        public int CompletedSteps { get; init; }
        public int TotalSteps { get; init; }
        public override string Kind => "run_completed";
    }

    /// <summary>The run errored.</summary>
    public sealed record RunErrorEvent : KoboldRunEvent
    {
        public string Message { get; init; } = "";
        public override string Kind => "run_error";
    }
}
