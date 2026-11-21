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
}
