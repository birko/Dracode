using DraCode.KoboldLair.Models.Tasks;

namespace DraCode.KoboldLair.Services.Checks
{
    /// <summary>
    /// One thing a checker found wrong with a finished task (TASK-217, FEATURE-078 D8): the same shape from every checker,
    /// so a finding reopens the task the way the acceptance check's own unmet promises do.
    /// </summary>
    /// <param name="Checker">The checker that found it.</param>
    /// <param name="Kind">What sort of finding, in the checker's words (e.g. "missing", "extra", "wrong").</param>
    /// <param name="Text">What is wrong, for the worker who fixes it.</param>
    /// <param name="Evidence">What it rests on: the requirement quoted, the file and line.</param>
    public sealed record TaskFinding(string Checker, string Kind, string Text, string? Evidence = null)
    {
        /// <summary>One line, as it is appended to the task and shown to a person.</summary>
        public override string ToString() =>
            $"[{Checker}] {Kind}: {Text}" + (string.IsNullOrWhiteSpace(Evidence) ? "" : $" (evidence: {Evidence})");
    }

    /// <summary>What a checker is given: the finished task, the folder its work is in, and its project.</summary>
    public sealed record TaskCheckInput(TaskRecord Task, string WorkspacePath, string ProjectId);

    /// <summary>
    /// A check every finished task goes through before Done (TASK-217, FEATURE-078 D8, D10, D30): a purpose-built agent,
    /// or anything that answers with findings. No finding means the task passes this check.
    /// </summary>
    public interface ITaskChecker
    {
        /// <summary>The checker's name, as findings and logs name it.</summary>
        string Name { get; }

        /// <summary>Its relative cost; cheaper checkers run first (D30).</summary>
        int Cost { get; }

        Task<IReadOnlyList<TaskFinding>> CheckAsync(TaskCheckInput input, CancellationToken cancellationToken = default);
    }

    /// <summary>What the checkers made of a task.</summary>
    /// <param name="Findings">What the first checker that found anything found; empty when every checker passed it.</param>
    /// <param name="Ran">The checkers that ran, in order.</param>
    /// <param name="CouldNotRun">Checkers that failed to run, each with why: a person judges in their place.</param>
    public sealed record TaskCheckReport(IReadOnlyList<TaskFinding> Findings, IReadOnlyList<string> Ran, IReadOnlyList<string> CouldNotRun)
    {
        public static readonly TaskCheckReport None = new([], [], []);
        public bool Passed => Findings.Count == 0 && CouldNotRun.Count == 0;
    }

    /// <summary>
    /// Runs the registered checkers on a finished task, cheapest first (TASK-217, D30), after the acceptance check's own
    /// checks. Every checker runs on every task; the run stops at the first checker with findings, since the task is
    /// reopened and every checker runs again on its next finish. A checker that throws is reported as unable to run,
    /// never as a pass. With no checker registered, nothing runs and the task is judged as before.
    /// </summary>
    public sealed class TaskCheckRunner
    {
        public TaskCheckRunner(IEnumerable<ITaskChecker>? checkers = null)
        {
            Checkers = (checkers ?? []).OrderBy(c => c.Cost).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
        }

        /// <summary>The checkers in the order they run.</summary>
        public IReadOnlyList<ITaskChecker> Checkers { get; }

        public async Task<TaskCheckReport> RunAsync(TaskCheckInput input, CancellationToken cancellationToken = default)
        {
            if (Checkers.Count == 0)
                return TaskCheckReport.None;

            var ran = new List<string>();
            var couldNotRun = new List<string>();
            foreach (var checker in Checkers)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ran.Add(checker.Name);
                IReadOnlyList<TaskFinding> findings;
                try
                {
                    findings = await checker.CheckAsync(input, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    couldNotRun.Add($"The {checker.Name} check could not run: {ex.Message}");
                    continue;
                }
                if (findings.Count > 0)
                    return new TaskCheckReport(findings, ran, couldNotRun);
            }
            return new TaskCheckReport([], ran, couldNotRun);
        }
    }
}
