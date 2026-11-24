namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>What <c>DrakeFactory.RetryFailedTaskAsync</c> did with a retry (TASK-153).</summary>
    public enum TaskRetryOutcome
    {
        /// <summary>The task was Failed and is Unassigned now, so Drake picks it up again.</summary>
        Retried,
        /// <summary>The task is not Failed; nothing changed.</summary>
        NotFailed,
        /// <summary>No Drake, task file or store row of the project holds the task.</summary>
        NotFound
    }

    /// <summary>
    /// A retry's result: what became of the task, its status when it was not retried, and the status its project was
    /// reopened from, or null when the project was still being worked (TASK-196).
    /// </summary>
    public sealed record TaskRetry(TaskRetryOutcome Outcome, TaskStatus? Status, Projects.ProjectStatus? ReopenedFrom = null)
    {
        public void Deconstruct(out TaskRetryOutcome outcome, out TaskStatus? status)
        {
            outcome = Outcome;
            status = Status;
        }
    }
}
