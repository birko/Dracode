using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Server.Models.WebSocket
{
    /// <summary>
    /// Flat wire frame emitted over the <c>/kobold</c> WebSocket (TASK-038). One shape covers every
    /// message; <see cref="Type"/> discriminates and only the relevant payload fields are populated
    /// (nulls are omitted on the wire — see the endpoint's serializer options). This is the transport
    /// projection of <see cref="KoboldRunEvent"/> plus the endpoint-synthesized <c>kobold_run_started</c>.
    /// </summary>
    public sealed record KoboldWireMessage
    {
        public string Type { get; init; } = "";
        public Guid RunId { get; init; }

        /// <summary>Monotonic per-run ordinal (0 for the endpoint-synthesized <c>kobold_run_started</c>).</summary>
        public int Seq { get; init; }

        // --- kobold_run_started ---
        public string? Mode { get; init; }
        public string? Worktree { get; init; }

        // --- kobold_tool_call ---
        public string? ToolName { get; init; }
        /// <summary><c>started</c> | <c>result</c>.</summary>
        public string? Phase { get; init; }
        public string? InputJson { get; init; }
        public string? ResultPreview { get; init; }

        // --- kobold_reflect ---
        public ReflectionEntry? Reflection { get; init; }
        public EscalationAlert? Escalation { get; init; }

        // --- kobold_stream (plan-step progress) ---
        public int? StepIndex { get; init; }
        public string? StepStatus { get; init; }
        public string? Output { get; init; }

        // --- progress counters (kobold_stream / kobold_complete) ---
        public int? CompletedSteps { get; init; }
        public int? TotalSteps { get; init; }

        // --- kobold_complete ---
        public string? FinalStatus { get; init; }

        // --- error ---
        public string? Message { get; init; }

        /// <summary>The first frame: the run was accepted and started.</summary>
        public static KoboldWireMessage RunStarted(Guid runId, string mode, string? worktree) => new()
        {
            Type = "kobold_run_started",
            RunId = runId,
            Seq = 0,
            Mode = mode,
            Worktree = worktree
        };

        /// <summary>A protocol/handler error surfaced to the client (terminal for the connection).</summary>
        public static KoboldWireMessage Error(Guid runId, string message) => new()
        {
            Type = "error",
            RunId = runId,
            Message = message
        };

        /// <summary>Projects a run-event-source record onto its wire frame.</summary>
        public static KoboldWireMessage From(KoboldRunEvent evt) => evt switch
        {
            ToolCallStartedEvent e => new KoboldWireMessage
            {
                Type = "kobold_tool_call", RunId = e.RunId, Seq = e.Sequence,
                Phase = "started", ToolName = e.ToolName, InputJson = e.InputJson
            },
            ToolCallResultEvent e => new KoboldWireMessage
            {
                Type = "kobold_tool_call", RunId = e.RunId, Seq = e.Sequence,
                Phase = "result", ToolName = e.ToolName, ResultPreview = e.ResultPreview
            },
            ReflectionEvent e => new KoboldWireMessage
            {
                Type = "kobold_reflect", RunId = e.RunId, Seq = e.Sequence,
                Reflection = e.Entry, Escalation = e.Escalation
            },
            PlanStepUpdatedEvent e => new KoboldWireMessage
            {
                Type = "kobold_stream", RunId = e.RunId, Seq = e.Sequence,
                StepIndex = e.StepIndex, StepStatus = e.Status.ToString(), Output = e.Output,
                CompletedSteps = e.CompletedSteps, TotalSteps = e.TotalSteps
            },
            RunCompletedEvent e => new KoboldWireMessage
            {
                Type = "kobold_complete", RunId = e.RunId, Seq = e.Sequence,
                FinalStatus = e.FinalStatus.ToString(),
                CompletedSteps = e.CompletedSteps, TotalSteps = e.TotalSteps
            },
            RunErrorEvent e => new KoboldWireMessage
            {
                Type = "error", RunId = e.RunId, Seq = e.Sequence, Message = e.Message
            },
            _ => new KoboldWireMessage { Type = evt.Kind, RunId = evt.RunId, Seq = evt.Sequence }
        };
    }
}
