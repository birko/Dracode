using DraCode.Agent;
using DraCode.KoboldTown.Wyvern;
using TaskStatus = DraCode.KoboldTown.Wyvern.TaskStatus;

namespace DraCode.KoboldTown.Agents
{
    /// <summary>
    /// Helper class for running tasks through the wyvern pattern.
    /// The wyvern analyzes the task and automatically delegates to the most appropriate specialized agent.
    /// </summary>
    public static class WyvernRunner
    {
        /// <summary>
        /// Runs a task through the wyvern, which will select and delegate to the appropriate specialized agent.
        /// Tracks task status and saves results to a markdown file.
        /// </summary>
        public static async Task<(string selectedAgentType, List<Message> wyvernConversation, List<Message>? delegatedConversation, TaskTracker tracker)> RunAsync(
            string provider,
            string task,
            AgentOptions? options = null,
            Dictionary<string, string>? config = null,
            string? outputMarkdownPath = null,
            int? maxIterations = null,
            Action<string, string>? messageCallback = null,
            TaskTracker? tracker = null)
        {
            options ??= new AgentOptions();
            tracker ??= new TaskTracker();
            
            // Add task to tracker
            var taskRecord = tracker.AddTask(task);
            
            // Save initial state
            if (outputMarkdownPath != null)
            {
                tracker.SaveToFile(outputMarkdownPath, "KoboldTown wyvern Task Report");
            }
            
            // Clear any previous selection
            SelectAgentTool.ClearSelection();

            try
            {
                // Create and run wyvern using KoboldTownAgentFactory
                var wyvern = KoboldTownAgentFactory.Create(provider, options, config, "wyvern");
                if (messageCallback != null)
                {
                    wyvern.SetMessageCallback(messageCallback);
                }

                messageCallback?.Invoke("info", "🎯 wyvern: Analyzing task to select the best agent...\n");
                
                var wyvernConversation = await wyvern.RunAsync(task, maxIterations: 5);

                // Get the agent selection
                var selection = SelectAgentTool.GetLastSelection();
                if (selection == null || !selection.TryGetValue("agent_type", out var agentTypeObj))
                {
                    tracker.SetError(taskRecord, "wyvern failed to select an agent");
                    if (outputMarkdownPath != null)
                    {
                        tracker.SaveToFile(outputMarkdownPath, "KoboldTown wyvern Task Report");
                    }
                    messageCallback?.Invoke("error", "wyvern failed to select an agent.");
                    return ("unknown", wyvernConversation, null, tracker);
                }

                var selectedAgentType = agentTypeObj.ToString()!;
                var delegatedTask = selection.TryGetValue("task", out var taskObj) ? taskObj.ToString()! : task;
                var reasoning = selection.TryGetValue("reasoning", out var reasoningObj) ? reasoningObj.ToString() : "No reasoning provided";

                // Update status: agent assigned
                tracker.UpdateTask(taskRecord, TaskStatus.NotInitialized, selectedAgentType);
                if (outputMarkdownPath != null)
                {
                    tracker.SaveToFile(outputMarkdownPath, "KoboldTown wyvern Task Report");
                }

                messageCallback?.Invoke("success", $"\n✓ Selected Agent: {selectedAgentType}");
                messageCallback?.Invoke("info", $"Reasoning: {reasoning}\n");
                messageCallback?.Invoke("info", $"🚀 Delegating to {selectedAgentType} agent...\n");

                // Update status: working
                tracker.UpdateTask(taskRecord, TaskStatus.Working);
                if (outputMarkdownPath != null)
                {
                    tracker.SaveToFile(outputMarkdownPath, "KoboldTown wyvern Task Report");
                }

                // Create and run the selected specialized agent using KoboldTownAgentFactory
                var specializedAgent = KoboldTownAgentFactory.Create(provider, options, config, selectedAgentType);
                if (messageCallback != null)
                {
                    specializedAgent.SetMessageCallback(messageCallback);
                }

                var delegatedConversation = await specializedAgent.RunAsync(delegatedTask, maxIterations);

                // Update status: done
                tracker.UpdateTask(taskRecord, TaskStatus.Done);
                if (outputMarkdownPath != null)
                {
                    tracker.SaveToFile(outputMarkdownPath, "KoboldTown wyvern Task Report");
                }

                messageCallback?.Invoke("success", $"\n✓ Task completed by {selectedAgentType} agent");

                return (selectedAgentType, wyvernConversation, delegatedConversation, tracker);
            }
            catch (Exception ex)
            {
                tracker.SetError(taskRecord, ex.Message);
                if (outputMarkdownPath != null)
                {
                    tracker.SaveToFile(outputMarkdownPath, "KoboldTown wyvern Task Report");
                }
                messageCallback?.Invoke("error", $"Error during orchestration: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Runs multiple tasks through the wyvern in sequence.
        /// All tasks are tracked in a single markdown report.
        /// </summary>
        public static async Task<(List<(string task, string agentType, List<Message>? conversation)> results, TaskTracker tracker)> RunMultipleAsync(
            string provider,
            IEnumerable<string> tasks,
            AgentOptions? options = null,
            Dictionary<string, string>? config = null,
            string? outputMarkdownPath = null,
            int? maxIterations = null,
            Action<string, string>? messageCallback = null)
        {
            var tracker = new TaskTracker();
            var results = new List<(string task, string agentType, List<Message>? conversation)>();

            foreach (var task in tasks)
            {
                var (agentType, _, conversation, _) = await RunAsync(
                    provider,
                    task,
                    options,
                    config,
                    outputMarkdownPath,
                    maxIterations,
                    messageCallback,
                    tracker
                );

                results.Add((task, agentType, conversation));
            }

            return (results, tracker);
        }

        /// <summary>
        /// Runs a task with just an wyvern to get agent recommendation without execution.
        /// Useful for testing or when you want to manually handle the delegation.
        /// </summary>
        public static async Task<(string? selectedAgentType, string? reasoning)> GetRecommendationAsync(
            string provider,
            string task,
            AgentOptions? options = null,
            Dictionary<string, string>? config = null,
            Action<string, string>? messageCallback = null)
        {
            options ??= new AgentOptions();
            
            SelectAgentTool.ClearSelection();

            var wyvern = KoboldTownAgentFactory.Create(provider, options, config, "wyvern");
            if (messageCallback != null)
            {
                wyvern.SetMessageCallback(messageCallback);
            }

            await wyvern.RunAsync(task, maxIterations: 5);

            var selection = SelectAgentTool.GetLastSelection();
            if (selection == null)
            {
                return (null, null);
            }

            var agentType = selection.TryGetValue("agent_type", out var at) ? at.ToString() : null;
            var reasoning = selection.TryGetValue("reasoning", out var r) ? r.ToString() : null;

            return (agentType, reasoning);
        }
    }
}
