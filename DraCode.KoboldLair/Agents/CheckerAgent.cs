using System.Text.Json;
using Birko.AI.Agents;
using Birko.AI;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Services.Checks;

namespace DraCode.KoboldLair.Agents
{
    /// <summary>
    /// The base of a purpose-built checker (TASK-217, FEATURE-078 D10): a tool-less agent that reads what its check needs,
    /// judges a finished task against one concern, and answers with findings in the one shape every checker shares. A
    /// checker supplies its role, its process, its rules and the material it reads; the base keeps the prompt conventions
    /// (role first, depth guidance, a numbered process with a stop, rules in priority order, the output schema in the
    /// message) and reads the reply. An unusable reply throws, so the check is reported as unable to run, never as a pass.
    /// </summary>
    public abstract class CheckerAgent : OrchestratorAgent, ITaskChecker
    {
        protected CheckerAgent(ILlmProvider provider, AgentOptions? options = null) : base(provider, options)
        {
        }

        public abstract string Name { get; }

        public abstract int Cost { get; }

        /// <summary>One line: who the checker is and what it judges, e.g. "the intent checker: did the task build what it asked".</summary>
        protected abstract string Role { get; }

        /// <summary>The numbered steps, ending with when to stop.</summary>
        protected abstract string Process { get; }

        /// <summary>The rules, in priority order.</summary>
        protected abstract string Rules { get; }

        /// <summary>What the checker reads beyond the task's description (files, a diff, the conventions), as text.</summary>
        protected abstract Task<string> GatherAsync(TaskCheckInput input, CancellationToken cancellationToken);

        protected override List<Tool> CreateTools() => new();

        protected override string SystemPrompt => $@"🔎 You are {Role}, one of the checks a finished task in the KoboldLair pipeline passes before it is done.

{GetDepthGuidance()}

## Process
{Process}

## Rules, in priority order
{Rules}
- Report only what the material shows. A finding you cannot point at is not a finding.
- No finding means the task passes this check; do not invent one to seem thorough.

Reply with the JSON object the message asks for, and nothing else.";

        public async Task<IReadOnlyList<TaskFinding>> CheckAsync(TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            var material = await GatherAsync(input, cancellationToken);
            var prompt = $@"## THE TASK
{input.Task.Task}

## WHAT YOU READ
{material}

## OUTPUT (JSON only)
{{ ""findings"": [ {{ ""kind"": ""a short word for what sort of finding"", ""text"": ""what is wrong, for the worker who fixes it"", ""evidence"": ""what it rests on: the requirement quoted, the file and line"" }} ] }}
An empty ""findings"" list means the task passes.";

            var messages = await RunAsync(prompt, maxIterations: 1, cancellationToken);
            return ReadFindings(Name, ExtractTextFromContent(messages.LastOrDefault(m => m.Role == "assistant")?.Content));
        }

        /// <summary>The findings in a checker's reply; throws <see cref="InvalidOperationException"/> when it has none to read.</summary>
        public static IReadOnlyList<TaskFinding> ReadFindings(string checker, string? reply)
        {
            if (string.IsNullOrWhiteSpace(reply))
                throw new InvalidOperationException("the checker gave no reply");
            try
            {
                using var json = JsonDocument.Parse(ExtractJsonFrom(reply));
                if (!json.RootElement.TryGetProperty("findings", out var list) || list.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("the checker's reply has no findings list");
                var findings = new List<TaskFinding>();
                foreach (var item in list.EnumerateArray())
                {
                    var text = Str(item, "text");
                    if (string.IsNullOrWhiteSpace(text))
                        continue;
                    findings.Add(new TaskFinding(checker, Str(item, "kind") is { Length: > 0 } kind ? kind : "finding", text, Str(item, "evidence")));
                }
                return findings;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"the checker's reply is not JSON: {ex.Message}");
            }
        }

        private static string? Str(JsonElement item, string name) =>
            item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()?.Trim()
                : null;

        /// <summary>The JSON object in a reply that may wrap it in a code fence or prose.</summary>
        private static string ExtractJsonFrom(string reply)
        {
            var start = reply.IndexOf('{');
            var end = reply.LastIndexOf('}');
            return start >= 0 && end > start ? reply[start..(end + 1)] : reply;
        }
    }
}
