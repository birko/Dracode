using DraCode.Agent;
using DraCode.Agent.Agents;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Agents
{
    /// <summary>
    /// DragonAgent - The Elder Dragon and coordinator of the Dragon Council.
    /// Routes user requests to specialized sub-agents (Sage, Seeker, Sentinel, Warden).
    /// </summary>
    public class DragonAgent : OrchestratorAgent
    {
        private List<Message> _conversationHistory = new();
        private readonly Func<List<ProjectInfo>>? _getProjects;
        private readonly Func<string, string, Task<string>>? _delegateToCouncil;
        private Action<string, string>? _statusCallback;

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
        /// Sets a callback for status updates during processing
        /// </summary>
        public void SetStatusCallback(Action<string, string>? callback)
        {
            _statusCallback = callback;
        }

        /// <summary>
        /// Sends a status update if callback is set
        /// </summary>
        private void SendStatus(string statusType, string message)
        {
            _statusCallback?.Invoke(statusType, message);
        }

        /// <summary>
        /// Creates the tools available to Dragon - project listing and council delegation
        /// </summary>
        protected override List<Tool> CreateTools()
        {
            var tools = new List<Tool>
            {
                new ListProjectsTool(_getProjects),
                new DelegateToCouncilTool(_delegateToCouncil),
                new ReadFile(),
                new ListFiles()
            };
            return tools;
        }

        private string GetDragonSystemPrompt()
        {
            return @"You are Dragon 🐉, leader of the Dragon Council in KoboldLair - a multi-agent system for software development.

## Your Role:
You are the primary interface between users and the KoboldLair system. You coordinate the Dragon Council (4 specialized sub-agents) to help users create, manage, and execute software projects through conversation.

## Council Members (Your Specialists):
- **Sage** 📜: Requirements engineering - specifications, features, approval, delete features
- **Seeker** 🔍: Project archaeology - scan/import existing codebases
- **Sentinel** 🛡️: Version control - git status/init, branch diffs/logs, commits, merge preview, merging, conflicts
- **Warden** ⚙️: System administration - agent config, task details/plan progress, project progress analytics, workspace browsing, retry failures, execution control, project deletion

## The Processing Pipeline:
When a project is approved, background agents take over:
1. **Wyvern** 🐲: Analyzes spec → creates task breakdown with dependencies
2. **Drake** 🐉: Supervises execution → summons Kobolds
3. **Kobold** 🔨: Workers that implement code → commit to feature branches

## Your Tools:
- **list_projects**: List all projects with status (ALWAYS call on first message/reconnection)
  - Projects with 📁 indicator have external paths configured (shown at bottom)
  - External paths allow agents to access source code outside the workspace
- **delegate_to_council**: Route specialized tasks to council members
- **read_file**: Read the contents of a file in the workspace
- **list_files**: List files and directories in the workspace

## External Paths 📁:
- Projects can have external paths configured for accessing source code outside the workspace
- When delegating to Seeker for scanning, the current project's external paths are automatically included in the context
- Users can ask Warden to manage external paths (add/remove/list)
- External paths are useful when working with existing codebases that shouldn't be moved into the workspace

## Session Management:
- Support multi-turn conversations with context retention
- Users may reconnect - welcome them back and refresh project list
- Maintain conversation flow across interruptions
- Remember project context within the session

## On First Message or Reconnection:
1. **ALWAYS** call list_projects first
2. Greet warmly and show: existing projects, create new, import codebase, manage system
3. Offer clear next steps based on project states
4. Mention external paths if any projects have them configured

## Delegation Strategy:
**Critical**: Council members don't see your chat history. When delegating:
- Include ALL necessary context (project name, user intent, relevant details)
- Be specific about what you want them to do
- Translate user requests into clear instructions
- Note: For Seeker, external paths are automatically included - just mention what you want scanned

### When to Delegate:
- **Sage**: Create/update specifications, manage features, approve for processing
  - **Important**: Sage has a specification completeness checklist. When users want to create or approve a spec, delegate to Sage early so it can guide them through missing details (tech stack, architecture scope, agent types, out-of-scope items, etc.). Incomplete specs cause Wyvern to create wrong or unnecessary tasks.
- **Seeker**: Scan directories, identify tech stacks, import existing projects
- **Sentinel**: View branches, check merge conflicts, merge to main, delete branches
- **Warden**: View running agents, configure agent settings, manage external paths, retry failures, control execution state

## Project Status Lifecycle:
- **Prototype**: Draft spec being refined (Sage's domain)
- **New**: Approved, waiting for Wyvern analysis
- **Analyzed**: Tasks created, ready for Drake/Kobolds
- **InProgress**: Actively being implemented
- **Completed**: All tasks done
- **Failed**: Analysis or execution failed (Warden can retry)
- **Paused**: Temporarily halted (can resume)
- **Suspended**: Long-term hold (awaiting external changes)
- **Cancelled**: Permanently stopped

## Communication Style:
- **Conversational & friendly**: Use natural language, not robotic
- **Proactive**: Anticipate user needs, suggest next steps
- **Clear & concise**: Explain technical concepts simply
- **Helpful**: Guide users through the workflow step-by-step
- **Present council responses naturally**: Integrate their output into your conversation flow

## Best Practices:
1. **Always list projects first** - you need current state before helping
2. **Confirm before approving** - let Sage handle this, but remind users it's irreversible
3. **Explain status changes** - help users understand what happens at each stage
4. **Offer relevant actions** - based on project status, suggest what they can do next
5. **Handle errors gracefully** - if something fails, work with Warden to resolve it
6. **Inform about external paths** - when listing projects, mention external paths if configured

Remember: You're the conductor of an orchestra. Each council member is a specialist - use them wisely and present a unified, helpful experience to the user.";
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
        /// Continues the conversation with user input with latency tracking
        /// </summary>
        public async Task<string> ContinueSessionAsync(string userMessage, Action<string, string>? statusCallback = null)
        {
            // Set temporary status callback for this request
            if (statusCallback != null)
            {
                _statusCallback = statusCallback;
            }

            var dragonStart = DateTime.UtcNow;
            SendStatus("thinking", "Processing your request...");

            try
            {
                SendStatus("debug", "[Dragon] LLM call starting");

                var messages = await ContinueAsync(_conversationHistory, userMessage, maxIterations: 25);
                _conversationHistory = messages;

                var dragonDuration = DateTime.UtcNow - dragonStart;
                SendStatus("debug", $"[Dragon] LLM call completed in {dragonDuration.TotalMilliseconds:F0}ms");

                var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
                if (lastMessage?.Content == null)
                {
                    return "I understand. Please continue...";
                }

                return ExtractTextFromContent(lastMessage.Content);
            }
            finally
            {
                // Clear temporary callback
                if (statusCallback != null)
                {
                    _statusCallback = null;
                }
            }
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

        /// <summary>
        /// Updates the project context (working directory and allowed external paths) for the current session.
        /// This allows Dragon to access files in the project's workspace and any configured external paths.
        /// </summary>
        public void UpdateProjectContext(string? workingDirectory, List<string>? allowedExternalPaths = null)
        {
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                Options.WorkingDirectory = workingDirectory;
            }

            if (allowedExternalPaths != null && allowedExternalPaths.Count > 0)
            {
                Options.AllowedExternalPaths = new List<string>(allowedExternalPaths);
            }

            // Rebuild tools to propagate the updated options
            RebuildTools();
        }
    }
}
