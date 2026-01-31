using DraCode.Agent;
using DraCode.Agent.Agents;
using DraCode.KoboldLair.Agents;

namespace DraCode.KoboldLair.Models.Agents
{
    /// <summary>
    /// Represents a Kobold worker agent that works on a specific task.
    /// Each Kobold is dedicated to one task and tracks its own status.
    /// </summary>
    public class Kobold
    {
        /// <summary>
        /// Unique identifier for this Kobold
        /// </summary>
        public Guid Id { get; }

        /// <summary>
        /// The agent instance created by KoboldLairAgentFactory
        /// </summary>
        public Agent.Agents.Agent Agent { get; }

        /// <summary>
        /// Type of agent (e.g., "csharp", "javascript", "react")
        /// </summary>
        public string AgentType { get; }

        /// <summary>
        /// Project identifier this Kobold belongs to
        /// </summary>
        public string? ProjectId { get; private set; }

        /// <summary>
        /// Task identifier this Kobold is assigned to (null if unassigned)
        /// </summary>
        public Guid? TaskId { get; private set; }

        /// <summary>
        /// Task description this Kobold is working on
        /// </summary>
        public string? TaskDescription { get; private set; }

        /// <summary>
        /// Specification context for the task (project requirements, architecture, etc.)
        /// </summary>
        public string? SpecificationContext { get; private set; }

        /// <summary>
        /// Current status of this Kobold
        /// </summary>
        public KoboldStatus Status { get; private set; }

        /// <summary>
        /// Timestamp when the Kobold was created
        /// </summary>
        public DateTime CreatedAt { get; }

        /// <summary>
        /// Timestamp when the Kobold was assigned to a task
        /// </summary>
        public DateTime? AssignedAt { get; private set; }

        /// <summary>
        /// Timestamp when the Kobold started working
        /// </summary>
        public DateTime? StartedAt { get; private set; }

        /// <summary>
        /// Timestamp when the Kobold completed the task
        /// </summary>
        public DateTime? CompletedAt { get; private set; }

        /// <summary>
        /// Error message if the Kobold encountered an error
        /// </summary>
        public string? ErrorMessage { get; private set; }

        /// <summary>
        /// Creates a new Kobold with an agent instance
        /// </summary>
        public Kobold(Agent.Agents.Agent agent, string agentType)
        {
            Id = Guid.NewGuid();
            Agent = agent;
            AgentType = agentType;
            Status = KoboldStatus.Unassigned;
            CreatedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Assigns this Kobold to a specific task
        /// </summary>
        /// <param name="taskId">Task identifier</param>
        /// <param name="taskDescription">Description of the task to execute</param>
        /// <param name="projectId">Project identifier this task belongs to</param>
        /// <param name="specificationContext">Optional specification context for the task</param>
        public void AssignTask(Guid taskId, string taskDescription, string? projectId = null, string? specificationContext = null)
        {
            if (Status != KoboldStatus.Unassigned)
            {
                throw new InvalidOperationException($"Cannot assign task to Kobold {Id} - current status is {Status}");
            }

            TaskId = taskId;
            TaskDescription = taskDescription;
            ProjectId = projectId;
            SpecificationContext = specificationContext;
            Status = KoboldStatus.Assigned;
            AssignedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Starts working on the assigned task and executes it through the underlying agent.
        /// Automatically transitions to Done on success or captures error on failure.
        /// </summary>
        /// <param name="maxIterations">Maximum iterations for agent execution</param>
        /// <returns>Messages from agent execution</returns>
        public async Task<List<Message>> StartWorkingAsync(int maxIterations = 30)
        {
            if (Status != KoboldStatus.Assigned)
            {
                throw new InvalidOperationException($"Cannot start working - Kobold {Id} is not assigned (current status: {Status})");
            }

            if (string.IsNullOrEmpty(TaskDescription))
            {
                throw new InvalidOperationException($"Cannot start working - Kobold {Id} has no task description");
            }

            Status = KoboldStatus.Working;
            StartedAt = DateTime.UtcNow;

            try
            {
                // Build the full task prompt with specification context if available
                var fullTaskPrompt = TaskDescription;
                
                if (!string.IsNullOrEmpty(SpecificationContext))
                {
                    fullTaskPrompt = $@"# Task Context

You are working on a task that is part of a larger project. Below is the project specification that provides important context for this task.

## Project Specification

{SpecificationContext}

---

## Your Task

{TaskDescription}

**Important**: Keep the project specification in mind while completing this task. Ensure your implementation aligns with the overall project requirements and architecture described above.";
                }

                // Execute the task through the underlying agent in non-interactive mode
                var messages = await Agent.RunAsync(fullTaskPrompt, maxIterations);
                
                // Task completed successfully - transition to Done
                Status = KoboldStatus.Done;
                CompletedAt = DateTime.UtcNow;
                
                return messages;
            }
            catch (Exception ex)
            {
                // Task failed - capture error and transition to Done
                ErrorMessage = ex.Message;
                Status = KoboldStatus.Done;
                CompletedAt = DateTime.UtcNow;
                throw; // Re-throw so caller knows it failed
            }
        }

        /// <summary>
        /// Gets whether this Kobold has completed its work (successfully or with error)
        /// </summary>
        public bool IsComplete => Status == KoboldStatus.Done;

        /// <summary>
        /// Gets whether this Kobold completed successfully (no error)
        /// </summary>
        public bool IsSuccess => Status == KoboldStatus.Done && string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Gets whether this Kobold failed (has error)
        /// </summary>
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        /// <summary>
        /// Resets the Kobold to unassigned state (for reuse)
        /// </summary>
        public void Reset()
        {
            TaskId = null;
            TaskDescription = null;
            ProjectId = null;
            SpecificationContext = null;
            Status = KoboldStatus.Unassigned;
            AssignedAt = null;
            StartedAt = null;
            CompletedAt = null;
            ErrorMessage = null;
        }

        /// <summary>
        /// Gets a string representation of this Kobold
        /// </summary>
        public override string ToString()
        {
            var taskInfo = TaskId.HasValue ? $"Task: {TaskId}" : "No task";
            return $"Kobold {Id.ToString()[..8]} ({AgentType}) - {Status} - {taskInfo}";
        }
    }
}
