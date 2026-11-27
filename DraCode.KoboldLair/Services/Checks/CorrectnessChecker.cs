using System.Text;
using Birko.AI.Providers;
using DraCode.KoboldLair.Agents;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services.Checks
{
    /// <summary>
    /// The correctness checker (TASK-221, FEATURE-078 D8): a review of a finished task's change for likely bugs — code that
    /// fails for an input or state it will meet — each named with its file and line and the failure it causes. It reads
    /// git's diff of the task's edits and the files it touched (<see cref="TaskMaterial"/>). It runs after the intent
    /// check, since there is no point reviewing code that does not yet do what was asked. With no model it cannot run,
    /// and the task goes to a person.
    /// </summary>
    public sealed class CorrectnessChecker : ITaskChecker
    {
        private readonly Func<string, ILlmProvider?> _providerFor;
        private readonly ILogger? _logger;

        /// <param name="providerFor">The model the check runs with, for a project.</param>
        public CorrectnessChecker(Func<string, ILlmProvider?> providerFor, ILogger? logger = null)
        {
            _providerFor = providerFor;
            _logger = logger;
        }

        public string Name => "correctness";

        /// <summary>One model call over the change and its files, after the intent check.</summary>
        public int Cost => 20;

        public async Task<IReadOnlyList<TaskFinding>> CheckAsync(TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            var provider = _providerFor(input.ProjectId)
                ?? throw new InvalidOperationException("no model is configured for the check");
            var findings = await new CorrectnessCheckerAgent(provider, await MaterialAsync(input, cancellationToken))
                .CheckAsync(input, cancellationToken);
            _logger?.LogInformation("Correctness check on task {TaskId}: {Count} finding(s)",
                input.Task.Id[..Math.Min(8, input.Task.Id.Length)], findings.Count);
            return findings;
        }

        /// <summary>What the check reads beyond the description: the diff of the task's edits, then the touched files.</summary>
        public static async Task<string> MaterialAsync(TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            var text = new StringBuilder();
            text.AppendLine("### What the task changed in files that existed before (git diff)");
            var diff = await TaskMaterial.DiffAsync(input.WorkspacePath, cancellationToken);
            if (diff == null)
                text.AppendLine("(no diff: the task's files are new, or the workspace is not under git; read the files below)");
            else
                text.AppendLine("```diff").AppendLine(diff).AppendLine("```");

            await TaskMaterial.AppendFilesAsync(text, input, cancellationToken);
            return text.ToString();
        }
    }

    /// <summary>The agent behind <see cref="CorrectnessChecker"/>: reviews one finished task's change for likely bugs.</summary>
    public sealed class CorrectnessCheckerAgent(ILlmProvider provider, string material) : CheckerAgent(provider)
    {
        public override string Name => "correctness";
        public override int Cost => 20;

        protected override string Role => "the correctness checker: does the task's change have bugs";

        protected override string Process => @"1. Read the diff to see what the task changed; read the files for what the changed code calls and is called by.
2. For each changed function or block, trace its inputs: empty, missing, zero, negative, very large, the wrong type, and the states it runs in.
3. Check what it calls: arguments in the right order and type, return values and errors handled, resources closed.
4. Stop: report each likely bug, or nothing.";

        protected override string Rules => @"1. ""bug"": code in the change that gives a wrong result, crashes or loses data for an input or state it will meet. Name the file and line, the input or state, and what goes wrong.
2. Judge only the change and the code it relies on; an old bug the change does not touch is not a finding.
3. Not a finding: style, naming, formatting, performance without a failure, missing tests, a missing feature (another check judges what was asked).
4. A failure you cannot state as ""this input → this wrong result"" is not a finding; ""might"" and ""could be cleaner"" are not bugs.
5. What these files cannot show (another module, the runtime, configuration) is not a finding.";

        protected override Task<string> GatherAsync(TaskCheckInput input, CancellationToken cancellationToken) =>
            Task.FromResult(material);
    }
}
