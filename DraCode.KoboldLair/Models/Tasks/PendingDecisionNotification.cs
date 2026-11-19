using System.Text.Json;

namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// The shape of a decision card as it travels in a project notification's metadata (TASK-020). The notification
    /// types and metadata keys are the contract TASK-024 renders and answers, and TASK-021 / TASK-025 reuse.
    /// </summary>
    public static class PendingDecisionNotification
    {
        public const string RequiredType = "decision_required";
        public const string ResolvedType = "decision_resolved";

        public const string RequiresResponse = "requiresResponse";
        public const string DecisionId = "decisionId";
        public const string TaskId = "taskId";
        public const string Source = "source";
        public const string Disposition = "disposition";
        public const string Question = "question";
        public const string EscalationType = "escalationType";
        public const string TimeoutAt = "timeoutAt";
        public const string ResponseSchema = "responseSchema";
        public const string ResolutionKind = "resolutionKind";

        /// <summary>The answer that hands an escalation back to its automatic route, as a timeout would.</summary>
        public const string HandleAutomatically = "Handle it automatically";

        public const string ResumeWithInstructions = "Resume with my instructions";

        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static Dictionary<string, string> ToMetadata(PendingDecision decision) => new()
        {
            [RequiresResponse] = "true",
            [DecisionId] = decision.Id,
            [TaskId] = decision.TaskId,
            [Source] = decision.Source.ToString(),
            [Disposition] = decision.Disposition.ToString(),
            [Question] = decision.Question,
            [EscalationType] = decision.Escalation?.Type.ToString() ?? "",
            [TimeoutAt] = decision.TimeoutAt?.ToString("O") ?? "",
            [ResponseSchema] = ResponseSchemaFor(decision)
        };

        public static Dictionary<string, string> ToResolvedMetadata(PendingDecision decision) => new()
        {
            [DecisionId] = decision.Id,
            [TaskId] = decision.TaskId,
            [ResolutionKind] = decision.ResolutionKind?.ToString() ?? ""
        };

        /// <summary>
        /// What the person may answer, as JSON: an escalation offers its automatic route or written instructions; any
        /// other decision is answered in free text until its own parker (TASK-021, TASK-025) defines choices.
        /// </summary>
        public static string ResponseSchemaFor(PendingDecision decision) =>
            JsonSerializer.Serialize(decision.Source == PendingDecisionSource.Escalation
                ? new ResponseSchemaShape("choice", new[] { HandleAutomatically, ResumeWithInstructions }, true)
                : new ResponseSchemaShape("text", Array.Empty<string>(), true), JsonOptions);

        private sealed record ResponseSchemaShape(string Kind, string[] Options, bool AllowFreeText);
    }
}
