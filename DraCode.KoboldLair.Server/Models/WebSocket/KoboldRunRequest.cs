using System.Text.Json;

namespace DraCode.KoboldLair.Server.Models.WebSocket
{
    /// <summary>
    /// Initial-message payload a client sends to the <c>/kobold</c> WebSocket endpoint (TASK-038).
    /// Carries the run <c>mode</c> plus loose passthrough fields the mode handlers read; the precise
    /// per-mode shapes (ad-hoc vs project) are owned by TASK-039 / TASK-040, so this stays permissive.
    /// </summary>
    public sealed class KoboldRunRequest
    {
        /// <summary>Dispatch discriminator: <c>adhoc</c> or <c>project</c>.</summary>
        public string? Mode { get; set; }

        /// <summary>Project-mode: the project to run a task for.</summary>
        public string? ProjectId { get; set; }

        /// <summary>Project-mode: the task to execute.</summary>
        public string? TaskId { get; set; }

        /// <summary>Ad-hoc mode: free-form task description.</summary>
        public string? Task { get; set; }

        /// <summary>Optional agent-type override (e.g. <c>csharp</c>).</summary>
        public string? AgentType { get; set; }

        /// <summary>Ad-hoc mode: working directory for the run.</summary>
        public string? WorkingDirectory { get; set; }

        /// <summary>Free-form per-mode options bag (validated by the mode handler).</summary>
        public JsonElement? Options { get; set; }
    }
}
