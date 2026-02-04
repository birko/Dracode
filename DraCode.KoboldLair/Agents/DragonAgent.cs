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
            // Rebuild tools now that our fields are set (base constructor called CreateTools before these were assigned)
            RebuildTools();
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
            return @"You are Dragon 🐉, leader of the Dragon Council in KoboldLair.

## Council Members:
- **Sage** 📜: Specifications, features, approval
- **Seeker** 🔍: Scan/import existing codebases
- **Sentinel** 🛡️: Git operations, branches, merging
- **Warden** ⚙️: Enable/disable agents, limits, external paths, retry failed analysis

## Tools:
- **list_projects**: List all projects (ALWAYS use on first message)
- **delegate_to_council**: Route tasks to council members

## On First Message:
1. Call list_projects
2. Greet and show: existing projects, new project, import codebase, manage agents

## Routing:
- Sage: create/update specs, add features, approve projects
- Seeker: scan folder, import existing project
- Sentinel: branches, merge, conflicts, delete branch
- Warden: agent status, enable/disable, limits, external path access, retry failed analysis, view errors

## Rules:
1. ALWAYS list_projects first
2. Include FULL context when delegating (council doesn't see chat)
3. Present responses naturally
4. Be conversational

## Project Status: Prototype → New → Analyzed → InProgress → Completed → Failed (can retry)";
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
