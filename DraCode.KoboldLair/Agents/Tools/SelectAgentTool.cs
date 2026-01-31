using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    public class SelectAgentTool : Tool
    {
        private readonly string _provider;
        private readonly Dictionary<string, string>? _config;

        public SelectAgentTool(string provider, Dictionary<string, string>? config)
        {
            _provider = provider;
            _config = config;
        }

        public override string Name => "select_agent";

        public override string Description => @"Select and instantiate the most appropriate specialized agent for the given task.

Parameters:
- agent_type (required): The type of agent to create. Must be one of: 'coding', 'csharp', 'cpp', 'assembler', 'javascript', 'typescript', 'css', 'html', 'react', 'angular', 'diagramming'
- reasoning (required): Brief explanation of why this agent was chosen (1-2 sentences)
- task (required): The original task description to pass to the selected agent

Returns: Information about the selected agent and confirmation that it will handle the task.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                agent_type = new
                {
                    type = "string",
                    description = "The type of specialized agent to instantiate. Must be one of: 'coding', 'csharp', 'cpp', 'assembler', 'javascript', 'typescript', 'css', 'html', 'react', 'angular', 'diagramming'"
                },
                reasoning = new
                {
                    type = "string",
                    description = "Brief explanation of why this agent type was selected (1-2 sentences)"
                },
                task = new
                {
                    type = "string",
                    description = "The task description to pass to the selected agent"
                }
            },
            required = new[] { "agent_type", "reasoning", "task" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> arguments)
        {
            try
            {
                if (!arguments.TryGetValue("agent_type", out var agentTypeObj) || agentTypeObj == null)
                {
                    return "Error: 'agent_type' parameter is required";
                }

                if (!arguments.TryGetValue("reasoning", out var reasoningObj) || reasoningObj == null)
                {
                    return "Error: 'reasoning' parameter is required";
                }

                if (!arguments.TryGetValue("task", out var taskObj) || taskObj == null)
                {
                    return "Error: 'task' parameter is required";
                }

                var agentType = agentTypeObj.ToString()!;
                var reasoning = reasoningObj.ToString()!;
                var task = taskObj.ToString()!;

                // Validate agent type
                var validAgentTypes = new[] { "coding", "csharp", "cpp", "assembler", "javascript", "typescript", "css", "html", "react", "angular", "php", "python", "diagramming", "media", "image", "svg", "bitmap" };
                if (!validAgentTypes.Contains(agentType.ToLowerInvariant()))
                {
                    return $"Error: Invalid agent_type '{agentType}'. Must be one of: {string.Join(", ", validAgentTypes)}";
                }

                // Store selection metadata for retrieval
                StoreSelection(agentType, reasoning, task);

                var result = $@"✓ Agent Selection Complete

Selected Agent: {agentType}
Reasoning: {reasoning}

The {agentType} agent is the best choice for this task.
Task will be delegated to the {agentType} agent for execution.

Original Task: {(task.Length > 200 ? task.Substring(0, 200) + "..." : task)}

To execute this selection, the system will now instantiate the {agentType} agent and pass it the task.";

                return result;
            }
            catch (Exception ex)
            {
                return $"Error selecting agent: {ex.Message}";
            }
        }

        private static readonly Dictionary<string, object> _lastSelection = new();

        private static void StoreSelection(string agentType, string reasoning, string task)
        {
            lock (_lastSelection)
            {
                _lastSelection["agent_type"] = agentType;
                _lastSelection["reasoning"] = reasoning;
                _lastSelection["task"] = task;
                _lastSelection["timestamp"] = DateTime.UtcNow;
            }
        }

        public static Dictionary<string, object>? GetLastSelection()
        {
            lock (_lastSelection)
            {
                return _lastSelection.Count > 0 ? new Dictionary<string, object>(_lastSelection) : null;
            }
        }

        public static void ClearSelection()
        {
            lock (_lastSelection)
            {
                _lastSelection.Clear();
            }
        }
    }
}
