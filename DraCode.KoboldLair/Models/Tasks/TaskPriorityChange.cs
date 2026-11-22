namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>What <c>DrakeFactory.SetTaskPriorityAsync</c> did with a priority change (TASK-154).</summary>
    public enum TaskPriorityOutcome
    {
        /// <summary>The task has the new priority wherever Drake reads it.</summary>
        Set,
        /// <summary>The task is Done; its priority no longer matters and was left alone.</summary>
        AlreadyDone,
        /// <summary>No Drake, task file or store row of the project holds the task.</summary>
        NotFound
    }

    /// <summary>The outcome, and for a task that was found, the task as it is now and the priority it had.</summary>
    public sealed record TaskPriorityChange(TaskPriorityOutcome Outcome, TaskRecord? Task = null, TaskPriority? Previous = null);
}
