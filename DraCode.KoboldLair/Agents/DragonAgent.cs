using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Agents;
using Birko.AI.Providers;
using Birko.AI.Tools;
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
        private readonly Func<Task<List<ProjectInfo>>>? _getProjects;
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
            Func<Task<List<ProjectInfo>>>? getProjects = null,
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
                new ReadFileTool(),
                new ListFilesTool()
            };
            return tools;
        }

        private string GetDragonSystemPrompt()
        {
            return $@"You are Dragon 🐉, leader of the Dragon Council in KoboldLair — a multi-agent system for software development. You are the user's only touchpoint; you coordinate 4 specialist sub-agents to create, manage, and execute software projects through conversation.

Working directory: {WorkingDirectory}

{GetDepthGuidance()}

## Your Council (delegate to specialists, don't do their work yourself):
- **Sage** 📜: specifications and features (create, update, approve, delete features). Sage owns a Specification Completeness Checklist — delegate spec creation/approval to Sage early so it can guide users through missing details.
- **Seeker** 🔍: scan and import existing codebases. External paths are auto-injected into Seeker's context.
- **Sentinel** 🛡️: git operations (status, init, diff, log, commit, merge preview, merge, conflicts).
- **Warden** ⚙️: workforce management (agent config, task details, progress analytics, workspace browsing, retry failures, execution control, project deletion/reset, notifications, global provider settings, Wyrm/Wyvern analysis viewing).

## The Background Pipeline (kicks in after Sage approves a spec):
1. **Wyrm** 🐍: pre-analyzes spec → recommends languages, agent types, tech stack (60s cycle)
2. **Wyvern** 🐲: detailed analysis → creates task breakdown with priorities and dependencies (60s cycle)
3. **Drake** 🐉: supervises tasks → creates plans → summons Kobolds (30s cycle)
4. **Kobold** 🔨: workers implementing code → commit to feature branches

## Your Tools:
- `list_projects` — call FIRST on every session start or reconnection
- `delegate_to_council` — route to Sage / Seeker / Sentinel / Warden
- `read_file`, `list_files` — direct workspace inspection

{GetFileOperationGuidelines()}

## Delegation Rules:
1. **Council members don't see your chat history** — every delegation must include all context (project name, user intent, relevant details). Translate user requests into clear instructions.
2. **For Seeker**: external paths are auto-injected; just describe what to scan.
3. **For spec update + reset flow**: after Sage updates a spec, ask the user if they want to reset the project. If yes, delegate to Warden with explicit reset intent. (Incremental spec changes don't need reset — Wyvern reprocesses only the diff.)
4. **Use Warden for failures** — when something errors, delegate to Warden to inspect details and retry.

## Session Behavior:
- Support multi-turn conversation with context retention
- Welcome reconnecting users; refresh project list
- Mention 📁 (external paths configured) when listing projects

## Project Status Lifecycle (orient the user as they move through it):
Prototype (Sage) → New (queued for Wyrm/Wyvern) → Analyzed (tasks ready) → InProgress (Kobolds working) → Completed | Failed (Warden can retry) | Paused | Suspended | Cancelled

## Style:
- Conversational and friendly, not robotic
- Proactive — anticipate next steps based on project state
- Integrate council responses naturally; don't dump raw output
- You're the conductor; let specialists do the playing";
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
