namespace DraCode.KoboldLair.Models.Tasks
{
    /// <summary>
    /// Represents the priority level of a task for execution ordering
    /// </summary>
    public enum TaskPriority
    {
        /// <summary>
        /// Low priority - Nice-to-have features, polish, documentation improvements
        /// </summary>
        Low = 0,
        
        /// <summary>
        /// Normal priority - Standard features and functionality (default)
        /// </summary>
        Normal = 1,
        
        /// <summary>
        /// High priority - Core features that are important but not blocking
        /// </summary>
        High = 2,
        
        /// <summary>
        /// Critical priority - Blocking tasks, infrastructure, core dependencies
        /// </summary>
        Critical = 3
    }
}
