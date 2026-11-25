namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// A level of the agent hierarchy a question climbs (FEATURE-078 D18, D24): it moves only up, each level gets it once,
    /// and the person is the end of the line. Stored as numbers on the pending decision's path, so the values are pinned.
    /// Never renumber; append.
    /// </summary>
    public enum QuestionLevel
    {
        /// <summary>The worker (Kobold) doing a task.</summary>
        Worker = 0,

        /// <summary>The task's supervisor (Drake).</summary>
        Supervisor = 1,

        /// <summary>The project's overseer (Wyvern).</summary>
        Overseer = 2,

        /// <summary>Dragon, the person's own agent.</summary>
        Dragon = 3,

        /// <summary>The person: the end of the line.</summary>
        Person = 4
    }
}
