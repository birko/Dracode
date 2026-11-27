using Birko.AI.Providers;
using DraCode.KoboldLair.Agents;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services.Checks
{
    /// <summary>
    /// The comment checker (TASK-220, FEATURE-078 D8; the lifecycle's review-comments): comments the task's change wrote
    /// that contradict the code (stale) or carry what already lives elsewhere (redundant: the code itself, the task or
    /// specification, git history), each naming where its content lives. It reads the change and the touched files
    /// (<see cref="TaskMaterial.ChangeAsync"/>) and runs last of the checkers: a comment matters least, and the run stops
    /// at the first checker with findings. With no model it cannot run, and the task goes to a person.
    /// </summary>
    public sealed class CommentChecker : ITaskChecker
    {
        private readonly Func<string, ILlmProvider?> _providerFor;
        private readonly ILogger? _logger;

        /// <param name="providerFor">The model the check runs with, for a project.</param>
        public CommentChecker(Func<string, ILlmProvider?> providerFor, ILogger? logger = null)
        {
            _providerFor = providerFor;
            _logger = logger;
        }

        public string Name => "comments";

        /// <summary>One model call over the change, after the intent and correctness checks.</summary>
        public int Cost => 30;

        public async Task<IReadOnlyList<TaskFinding>> CheckAsync(TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            var provider = _providerFor(input.ProjectId)
                ?? throw new InvalidOperationException("no model is configured for the check");
            var findings = await new CommentCheckerAgent(provider, await TaskMaterial.ChangeAsync(input, cancellationToken))
                .CheckAsync(input, cancellationToken);
            _logger?.LogInformation("Comment check on task {TaskId}: {Count} finding(s)",
                input.Task.Id[..Math.Min(8, input.Task.Id.Length)], findings.Count);
            return findings;
        }
    }

    /// <summary>The agent behind <see cref="CommentChecker"/>: judges the comments one finished task's change wrote.</summary>
    public sealed class CommentCheckerAgent(ILlmProvider provider, string material) : CheckerAgent(provider)
    {
        public override string Name => "comments";
        public override int Cost => 30;

        protected override string Role => "the comment checker: is each comment the change wrote true, and the right place for what it says";

        protected override string Process => @"1. Find the comments the change wrote: lines the diff adds or changes, and every comment in a file the task created. Judge each whole comment block, with the code it sits above.
2. Compare each with the code: does it still say what the code does?
3. Ask where its content already lives: the code itself, the task's description or the specification, or git history (what changed, when, for which task).
4. Stop: report each stale or redundant comment, or nothing.";

        protected override string Rules => @"1. ""stale"": a comment that contradicts the code. Quote it, name the file and line, and say what the code does instead.
2. ""redundant"": a comment whose content already lives elsewhere: it restates the code beside it, repeats the task or specification, or narrates the change history. Quote it, name the file and line, and write ""lives in:"" with where.
3. Not a finding: a comment that says why (a reason, a trade-off, a constraint the code cannot show), a public API's documentation, a licence header, or a comment's length alone.
4. Judge only comments the change wrote; one it did not touch is not a finding.";

        protected override Task<string> GatherAsync(Checks.TaskCheckInput input, CancellationToken cancellationToken) =>
            Task.FromResult(material);
    }
}
