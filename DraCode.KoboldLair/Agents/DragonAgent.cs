using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Projects;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Agents
{
    /// <summary>
    /// DragonAgent - The Elder Dragon and coordinator of the Dragon Council.
    /// Routes user requests to specialized sub-agents (Sage, Seeker, Sentinel, Warden).
    /// </summary>
    public class DragonAgent : AgentBase
    {
        private List<Message> _conversationHistory = new();
        private readonly Func<List<ProjectInfo>>? _getProjects;
        private readonly Func<string, string, Task<string>>? _delegateToCouncil;

        protected override string SystemPrompt => GetDragonSystemPrompt();

        /// <summary>
        /// Creates a new Dragon agent (coordinator)
        /// </summary>
        /// <param name="provider">LLM provider to use</param>
        /// <param name="options">Agent options</param>
        /// <param name="getProjects">Function to get list of all projects for listing</param>
        /// <param name="delegateToCouncil">Function to delegate tasks to council members (memberName, task) => result</param>
        public DragonAgent(
            ILlmProvider provider,
            AgentOptions? options = null,
            Func<List<ProjectInfo>>? getProjects = null,
            Func<string, string, Task<string>>? delegateToCouncil = null)
            : base(provider, options)
        {
            _getProjects = getProjects;
            _delegateToCouncil = delegateToCouncil;
        }

        /// <summary>
        /// Creates the tools available to Dragon - project listing and council delegation
        /// </summary>
        protected override List<Tool> CreateTools()
        {
            var tools = new List<Tool>
            {
                new ListProjectsTool(_getProjects),
                new DelegateToCouncilTool(_delegateToCouncil)
            };
            return tools;
        }

        private string GetDragonSystemPrompt()
        {
            return @"You are Dragon 🐉, the Elder Dragon and leader of the Dragon Council in KoboldLair.

You coordinate a team of specialized sub-agents (your Council) to help users with their projects.

## The Dragon Council:

| Member | Role | Handles |
|--------|------|---------|
| **Sage** 📜 | Lore Keeper | Specifications, features, project approval |
| **Seeker** 🔍 | Project Scout | Scanning existing codebases, importing projects |
| **Sentinel** 🛡️ | Code Guardian | Git operations, branches, merging |
| **Warden** ⚙️ | Agent Overseer | Enable/disable agents, set limits |

## Your Tools:
- **list_projects**: List all projects (use on welcome)
- **delegate_to_council**: Send tasks to council members

## Welcome Behavior (ALWAYS do this first):
1. Use 'list_projects' to see existing projects
2. Greet warmly and show options:
   - Continue existing project
   - Start new project
   - Import existing codebase
   - Manage agents

Example:
""Hello! I'm Dragon 🐉, leader of the Dragon Council.

Your projects:
- **TodoApp** (Prototype)
- **WebStore** (In Progress)

How can we help?
1. Work on existing project
2. Create new project
3. Import existing codebase
4. Manage background agents""

## Routing Guide:

**→ Sage** (specifications/features):
- ""Create a new project""
- ""Add a feature to X""
- ""Update the specification""
- ""Approve the project""
- ""Show me the spec for X""

**→ Seeker** (import projects):
- ""Scan my project at C:\path""
- ""Import an existing codebase""
- ""Analyze this folder""

**→ Sentinel** (git operations):
- ""Show me the branches""
- ""Merge the feature branch""
- ""Check for conflicts""
- ""Delete merged branch""

**→ Warden** (agent config, external paths):
- ""Show agent status""
- ""Enable Wyvern for X""
- ""Disable kobolds""
- ""Set parallel limit to 3""
- ""Allow access to C:\path""
- ""Grant file access to external folder""
- ""Show allowed paths for project""
- ""Remove external path access""

## How to Delegate:

Use delegate_to_council with:
- council_member: sage, seeker, sentinel, or warden
- task: Detailed description with ALL context the user provided

Example delegation:
```
delegate_to_council(
  council_member: ""sage"",
  task: ""The user wants to create a new project called 'TodoApp'. It should be a React web application with user authentication and task management features.""
)
```

## Important Rules:
1. **ALWAYS list_projects on first message**
2. **Include full context** when delegating - the council member doesn't see chat history
3. **Present council responses** naturally to the user
4. For multi-step tasks, you may need multiple delegations
5. Be conversational and friendly

## Project Status Reference:
- **Prototype**: Spec created, needs approval
- **New**: Approved, ready for Wyvern
- **Analyzed**: Tasks created
- **InProgress**: Kobolds working
- **Completed**: Done";
        }

        /// <summary>
        /// Starts an interactive session with the user
        /// </summary>
        public async Task<string> StartSessionAsync(string? initialMessage = null)
        {
            if (string.IsNullOrEmpty(initialMessage))
            {
                initialMessage = "Hello, I just connected. Please welcome me and show me my options.";
            }

            _conversationHistory = new List<Message>();
            var messages = await RunAsync(initialMessage, maxIterations: 15);
            _conversationHistory = messages;

            var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
            if (lastMessage?.Content == null)
            {
                return "Hello! I'm Dragon 🐉, leader of the Dragon Council. How can we help you today?";
            }

            return ExtractTextFromContent(lastMessage.Content);
        }

        /// <summary>
        /// Continues the conversation with user input
        /// </summary>
        public async Task<string> ContinueSessionAsync(string userMessage)
        {
            var messages = await ContinueAsync(_conversationHistory, userMessage, maxIterations: 25);
            _conversationHistory = messages;

            var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
            if (lastMessage?.Content == null)
            {
                return "I understand. Please continue...";
            }

            return ExtractTextFromContent(lastMessage.Content);
        }

        private string ExtractTextFromContent(object content)
        {
            if (content is string text) return text;
            if (content is ContentBlock block) return block.Text ?? "";
            if (content is IEnumerable<ContentBlock> blocks)
            {
                return string.Join("\n", blocks
                    .Where(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
                    .Select(b => b.Text));
            }
            return content?.ToString() ?? "";
        }

        /// <summary>
        /// Gets the number of messages in the current conversation
        /// </summary>
        public int ConversationMessageCount => _conversationHistory.Count;

        /// <summary>
        /// Clears the conversation history
        /// </summary>
        public void ClearConversationHistory()
        {
            _conversationHistory = new List<Message>();
        }

        /// <summary>
        /// Clears the agent's context/conversation history without reloading
        /// </summary>
        public void ClearContext()
        {
            ClearConversationHistory();
        }

        /// <summary>
        /// Restores conversation context from a list of messages
        /// </summary>
        public void RestoreContext(IEnumerable<(string Role, string Content)> messages)
        {
            foreach (var (role, content) in messages)
            {
                _conversationHistory.Add(new Message { Role = role, Content = content });
            }
        }
    }
}
