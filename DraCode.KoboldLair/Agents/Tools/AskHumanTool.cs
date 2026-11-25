using System.Text.Json;
using Birko.AI.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Lets a Kobold ask a person a question during plan execution (TASK-021, FEATURE-028). Drake parks the task on the
    /// question; this run then ends after the current turn, and the task resumes with the answer once a person gives it.
    /// One instance per run, like <see cref="ReflectionTool"/>.
    /// </summary>
    public class AskHumanTool : Tool
    {
        private readonly KoboldImplementationPlan _plan;
        private readonly KoboldPlanService? _planService;
        private readonly ILogger? _logger;
        private readonly string? _projectId;
        private readonly string? _taskId;
        private readonly Guid _koboldId;
        private readonly Func<AskHumanQuestion, CancellationToken, Task<AskHumanOutcome>> _ask;
        private readonly Action _stopAfterTurn;

        // A person reads the card: keep what the model writes to a readable size
        internal const int MaxQuestionLength = 1000;
        internal const int MaxContextLength = 2000;
        internal const int MaxOptions = 10;
        internal const int MaxOptionLength = 200;

        public AskHumanTool(
            KoboldImplementationPlan plan,
            KoboldPlanService? planService,
            ILogger? logger,
            string? projectId,
            string? taskId,
            Guid koboldId,
            Func<AskHumanQuestion, CancellationToken, Task<AskHumanOutcome>> ask,
            Action stopAfterTurn)
        {
            _plan = plan;
            _planService = planService;
            _logger = logger;
            _projectId = projectId ?? plan.ProjectId;
            _taskId = taskId ?? plan.TaskId;
            _koboldId = koboldId;
            _ask = ask;
            _stopAfterTurn = stopAfterTurn;
        }

        public override string Name => "ask_human";

        public override string Description => @"Ask a person a question when a real ambiguity blocks correct work and guessing would likely be wrong.
Your task pauses until the person answers; the answer arrives when the work resumes. Make this the LAST tool call of your turn: nothing after it is worth doing before the answer.
Ask only what the specification, the plan and the code cannot tell you. If the question is refused, decide yourself, record the assumption with reflect, and continue.

Parameters:
- question (required): the question, answerable on its own
- options (optional): the choices you offer
- allow_free_text (optional): whether an answer other than an option is accepted (default: true without options, false with them)
- context (optional): why you are asking — what you found and what each answer would change

Returns: whether the question was sent (and the task paused) or refused.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                question = new { type = "string", description = "The question, answerable on its own" },
                options = new { type = "array", items = new { type = "string" }, description = "The choices offered" },
                allow_free_text = new { type = "boolean", description = "Accept an answer other than an option" },
                context = new { type = "string", description = "Why you are asking" }
            },
            required = new[] { "question" }
        };

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> arguments,
            CancellationToken cancellationToken = default)
        {
            var text = ReadString(arguments, "question")?.Trim();
            var options = ReadStrings(arguments, "options").Select(o => o.Trim()).Where(o => o.Length > 0).ToArray();
            var allowFreeText = ReadBool(arguments, "allow_free_text") ?? options.Length == 0;
            var context = ReadString(arguments, "context")?.Trim();

            if (Problem(text, options, allowFreeText, context) is { } problem)
                return $"Error: {problem}";

            var question = new AskHumanQuestion(text!, options, allowFreeText, string.IsNullOrEmpty(context) ? null : context);
            _plan.AddLogEntry(OneLine($"ask_human: \"{text}\"" + (options.Length > 0 ? $" [{string.Join(" | ", options)}]" : "")
                + (question.Context != null ? $" — {question.Context}" : "")));

            AskHumanOutcome outcome;
            try
            {
                outcome = await _ask(question, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger?.LogError(ex, "ask_human failed for task {TaskId}", _taskId);
                outcome = new AskHumanOutcome(false, null, "the question could not be sent");
            }

            _plan.AddLogEntry(outcome.Parked
                ? $"→ decision {outcome.DecisionId}, task parked"
                : $"→ not asked: {outcome.Refusal}");
            await SavePlanAsync();

            if (!outcome.Parked)
                return $"Not asked: {outcome.Refusal}. Decide yourself, record the assumption with reflect, and continue.";

            _stopAfterTurn();
            _logger?.LogInformation("Kobold {KoboldId} asked a person (decision {DecisionId}) for task {TaskId}",
                _koboldId.ToString()[..8], outcome.DecisionId, _taskId);
            return $"Question sent (decision {outcome.DecisionId}). Your task is paused until a person answers; this run ends after this turn.";
        }

        /// <summary>
        /// What is wrong with a question a person would be asked, or null when it can be asked: the checks every asker
        /// shares, the Kobold's tool and the analyzer's questions (TASK-170) alike. Options are expected trimmed.
        /// </summary>
        public static string? Problem(string? text, string[] options, bool allowFreeText, string? context)
        {
            if (string.IsNullOrEmpty(text))
                return "question is required.";
            if (options.Length == 0 && !allowFreeText)
                return "a question with no options must allow free text.";
            if (options.Distinct(StringComparer.Ordinal).Count() != options.Length)
                return "the options must be distinct.";
            if (text.Length > MaxQuestionLength)
                return $"keep the question under {MaxQuestionLength} characters.";
            if (options.Length > MaxOptions || options.Any(o => o.Length > MaxOptionLength))
                return $"offer at most {MaxOptions} options of up to {MaxOptionLength} characters each.";
            if (context?.Length > MaxContextLength)
                return $"keep the context under {MaxContextLength} characters.";
            return null;
        }

        /// <summary>The plan log is a markdown list: one entry, one line.</summary>
        private static string OneLine(string text) =>
            string.Join(' ', text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));

        private async Task SavePlanAsync()
        {
            if (_planService == null || string.IsNullOrEmpty(_projectId))
                return;
            try
            {
                await _planService.SavePlanAsync(_plan);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Could not save the plan log for task {TaskId}", _taskId);
            }
        }

        private static string? ReadString(Dictionary<string, object> arguments, string key)
        {
            if (!arguments.TryGetValue(key, out var value) || value == null) return null;
            return value is JsonElement { ValueKind: JsonValueKind.String } je ? je.GetString() : value.ToString();
        }

        private static bool? ReadBool(Dictionary<string, object> arguments, string key)
        {
            if (!arguments.TryGetValue(key, out var value) || value == null) return null;
            return value switch
            {
                bool b => b,
                JsonElement { ValueKind: JsonValueKind.True } => true,
                JsonElement { ValueKind: JsonValueKind.False } => false,
                _ => bool.TryParse(value.ToString(), out var parsed) ? parsed : null
            };
        }

        private static IEnumerable<string> ReadStrings(Dictionary<string, object> arguments, string key)
        {
            if (!arguments.TryGetValue(key, out var value) || value == null) return Array.Empty<string>();
            return value switch
            {
                JsonElement { ValueKind: JsonValueKind.Array } je => je.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!),
                IEnumerable<string> list => list,
                _ => Array.Empty<string>()
            };
        }
    }
}
