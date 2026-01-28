using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Server.Agents.Wyvern
{
    public class WyvernAgent : AgentBase
    {
        private readonly string _provider;
        private readonly Dictionary<string, string>? _config;

        public WyvernAgent(ILlmProvider llmProvider, AgentOptions? options = null, string provider = "openai", Dictionary<string, string>? config = null)
            : base(llmProvider, options)
        {
            _provider = provider;
            _config = config;
        }

        protected override string SystemPrompt
        {
            get
            {
                var depthGuidance = Options.ModelDepth switch
                {
                    <= 3 => @"
Reasoning approach: Quick and efficient
- Make direct, straightforward decisions
- Prioritize speed over exhaustive analysis",
                    >= 7 => @"
Reasoning approach: Deep and thorough
- Think carefully through task requirements
- Consider multiple aspects before deciding",
                    _ => @"
Reasoning approach: Balanced
- Analyze the task requirements
- Choose the most appropriate agent"
                };

                return $@"You are an intelligent task wyvern working in a sandboxed workspace at {WorkingDirectory}.

Your role is to analyze task descriptions and decide which specialized agent should handle them.

Available specialized agents:
1. 'coding' - General coding tasks, multiple languages, when no specific specialization is clear
2. 'csharp' - C# and .NET development tasks (ASP.NET Core, Entity Framework, Blazor, etc.)
3. 'cpp' - C++ development tasks (modern C++, STL, CMake, performance optimization)
4. 'assembler' - Assembly language tasks (x86/x64, ARM, low-level programming)
5. 'javascript' or 'typescript' - Vanilla JavaScript/TypeScript tasks (no frameworks, Node.js, DOM)
6. 'css' - CSS styling tasks (Grid, Flexbox, animations, responsive design)
7. 'html' - HTML markup tasks (semantic HTML5, accessibility, SEO)
8. 'react' - React development tasks (hooks, components, state management)
9. 'angular' - Angular development tasks (TypeScript, RxJS, dependency injection)
10. 'php' - PHP development tasks (Laravel, Symfony, WordPress, PSR standards)
11. 'python' - Python development tasks (Django, Flask, data science, machine learning)
12. 'diagramming' - Creating diagrams (UML, ERD, DFD, user stories, activity diagrams)
13. 'media' - General media tasks (images, video, audio, formats, optimization)
14. 'image' - Image tasks (raster and vector, editing, formats)
15. 'svg' - SVG creation/editing tasks (scalable vector graphics, icons, illustrations)
16. 'bitmap' - Bitmap/raster image tasks (JPEG, PNG, WebP, photo editing, compression)

{depthGuidance}

When you receive a task:
1. Analyze the task description carefully
2. Identify the primary technology or goal
3. Use the 'select_agent' tool to choose the most appropriate agent
4. The selected agent will then be instantiated to handle the actual work

Selection guidelines:
- If task mentions specific framework/language (React, Angular, C#, C++, PHP, Python), choose that agent
- If task is about creating diagrams or visual models, choose 'diagramming'
- If task involves styling/layout/design, choose 'css' or 'html'
- If task involves images (SVG, icons, photos, editing), choose 'svg', 'bitmap', or 'image'
- If task involves general media (video, audio, formats), choose 'media'
- If task is general coding or multiple languages, choose 'coding'
- If task is about pure JavaScript without frameworks, choose 'javascript'
- Be decisive and choose the single most appropriate agent

You must call the 'select_agent' tool to make your decision.";
            }
        }

        protected override List<Tool> CreateTools()
        {
            var tools = base.CreateTools();
            tools.Add(new SelectAgentTool(_provider, _config));
            return tools;
        }
    }

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
