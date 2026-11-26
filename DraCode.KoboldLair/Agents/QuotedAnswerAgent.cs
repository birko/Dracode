using Birko.AI;
using Birko.AI.Agents;
using Birko.AI.Providers;
using Birko.AI.Tools;

namespace DraCode.KoboldLair.Agents
{
    /// <summary>
    /// Answers a question at one level of the question chain (FEATURE-078 D18, D19) only from the sources that level owns,
    /// quoting the line that settles it; anything those sources do not settle it leaves unanswered, so the question goes
    /// up. Used by the supervisor's level (Drake, TASK-203) and Dragon's (TASK-206). No tools: it reads what it is given.
    /// </summary>
    public class QuotedAnswerAgent : OrchestratorAgent
    {
        private readonly string _role;
        private readonly string _sources;
        private readonly string _otherwise;

        /// <param name="role">Who answers, e.g. "Drake 🐉, the supervisor of one task".</param>
        /// <param name="sources">What the sources are, e.g. "the task's description, its plan, …".</param>
        /// <param name="otherwise">Where a declined question goes, e.g. "the project's overseer, and maybe a person".</param>
        public QuotedAnswerAgent(ILlmProvider provider, string role, string sources, string otherwise, AgentOptions? options = null)
            : base(provider, options)
        {
            _role = role;
            _sources = sources;
            _otherwise = otherwise;
        }

        protected override List<Tool> CreateTools() => new();

        protected override string SystemPrompt => $@"You are {_role} in the KoboldLair pipeline. A question an agent below you could not settle has climbed to you; you answer it only from what your level owns.

{GetDepthGuidance()}

## Process
1. Read the question, then the sources you are given: {_sources}.
2. Find a line in those sources that settles the question on its own.
3. Stop: answer with that line quoted, or decline.

## Rules, in priority order
1. Answer only from the sources. Never from general knowledge, preference or what seems sensible: a guess here becomes a decision nobody made.
2. Quote the line that settles it, word for word, as it appears in the sources. No line, no answer.
3. When the question offers options and no free text, the answer must be one of the options, as written.
4. When the sources are silent, ambiguous or in conflict, decline: the question goes to {_otherwise}.

Reply with the JSON object the message asks for, and nothing else.";

        /// <summary>
        /// The answer the sources give, with its quote; null when they do not settle the question (or the reply is unusable).
        /// </summary>
        public async Task<(string Answer, string Quote)?> AnswerAsync(string question, string sources, CancellationToken cancellationToken = default)
        {
            var prompt = $@"## QUESTION
{question}

## SOURCES
{sources}

## OUTPUT (JSON only)
{{ ""answer"": ""the answer, or null when the sources do not settle it"", ""quote"": ""the line from the sources that settles it, word for word"" }}";

            var messages = await RunAsync(prompt, maxIterations: 1, cancellationToken);
            var text = ExtractTextFromContent(messages.LastOrDefault(m => m.Role == "assistant")?.Content);
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(ExtractJson(text));
                var root = json.RootElement;
                var answer = root.TryGetProperty("answer", out var a) && a.ValueKind == System.Text.Json.JsonValueKind.String ? a.GetString() : null;
                var quote = root.TryGetProperty("quote", out var q) && q.ValueKind == System.Text.Json.JsonValueKind.String ? q.GetString() : null;
                return string.IsNullOrWhiteSpace(answer) || string.IsNullOrWhiteSpace(quote) ? null : (answer.Trim(), quote.Trim());
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException)
            {
                return null;
            }
        }
    }
}
