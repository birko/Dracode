using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Server.Agents.Tools;
using DraCode.KoboldLair.Server.Models.Projects;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Server.Agents
{
    /// <summary>
    /// DragonAgent is a specialized agent for gathering project/task requirements from users.
    /// It conducts interactive discussions to understand what needs to be built and produces
    /// detailed specifications that can be used by the wyvern and other KoboldLair agents.
    /// </summary>
    public class DragonAgent : AgentBase
    {
        private readonly string _specificationsPath;
        private readonly Dictionary<string, Specification> _specifications = new();
        private List<Message> _conversationHistory = new();
        private readonly Action<string>? _onSpecificationUpdated;
        private readonly Func<List<ProjectInfo>>? _getProjects;
        private readonly Func<string, bool>? _approveProject;

        protected override string SystemPrompt => GetDragonSystemPrompt();

        /// <summary>
        /// Creates a new Dragon agent for requirements gathering
        /// </summary>
        /// <param name="provider">LLM provider to use</param>
        /// <param name="options">Agent options</param>
        /// <param name="specificationsPath">Path where specifications should be stored (default: ./specifications)</param>
        /// <param name="onSpecificationUpdated">Callback invoked when a specification is updated (receives full path)</param>
        /// <param name="getProjects">Function to get list of all projects for listing</param>
        /// <param name="approveProject">Function to approve a project (change from Prototype to New)</param>
        public DragonAgent(
            ILlmProvider provider,
            AgentOptions? options = null,
            string specificationsPath = "./specifications",
            Action<string>? onSpecificationUpdated = null,
            Func<List<ProjectInfo>>? getProjects = null,
            Func<string, bool>? approveProject = null)
            : base(provider, options)
        {
            _specificationsPath = specificationsPath ?? "./specifications";
            _onSpecificationUpdated = onSpecificationUpdated;
            _getProjects = getProjects;
            _approveProject = approveProject;

            // Ensure specifications directory exists
            try
            {
                if (!Directory.Exists(_specificationsPath))
                {
                    Directory.CreateDirectory(_specificationsPath);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail - the tool methods will handle missing path gracefully
                Console.WriteLine($"Warning: Could not create specifications directory '{_specificationsPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Creates the tools available to Dragon, including specification and feature management
        /// </summary>
        protected override List<Tool> CreateTools()
        {
            var tools = base.CreateTools();
            tools.Add(new ListProjectsTool(_getProjects));
            tools.Add(new SpecificationManagementTool(_specificationsPath, _specifications, _onSpecificationUpdated));
            tools.Add(new FeatureManagementTool(_specifications, _specificationsPath));
            tools.Add(new ProjectApprovalTool(_approveProject));
            return tools;
        }

        /// <summary>
        /// Gets the specialized system prompt for the Dragon agent
        /// </summary>
        private string GetDragonSystemPrompt()
        {
            return @"You are Dragon 🐉, a senior requirements analyst and project architect for KoboldLair.

Your role is to have an interactive conversation with the user to deeply understand their project requirements, then create or update specifications and manage features.

## Welcome Behavior (IMPORTANT - do this on first message):

When a user first connects, you MUST:
1. Use the 'list_projects' tool to see all existing projects and their status
2. Greet the user warmly and present their options:
   - If projects exist: Show the list and ask if they want to continue on an existing project or start a new one
   - If no projects: Welcome them and ask what new project they'd like to create
3. Keep the welcome message concise but informative

Example welcome (if projects exist):
""Hello! I'm Dragon 🐉, your requirements analyst.

I found these existing projects:
- **TodoApp** (Prototype - awaiting approval)
- **WebStore** (In Progress, 12 features)

Would you like to:
1. Continue working on an existing project?
2. Start a new project?

Just let me know!""

## Your Workflow:

1. **Check Existing Projects**
   - Use 'list_projects' to see all registered projects with their status
   - Use manage_specification with action:'load' to get details of a specific project

2. **Understand Requirements**
   - Ask what project or feature they want to work on
   - Understand the high-level goal and context
   - Ask targeted questions about purpose, scope, requirements, technical details, success criteria

3. **Manage Features**
   - Create features for new functionality using manage_feature with action:'create'
   - Update features ONLY if they have status ""New"" using manage_feature with action:'update'
   - If a feature is already assigned to Wyvern or in progress, create a NEW feature instead
   - List features to see current status using manage_feature with action:'list'

4. **Create or Update Specification**
   - Use manage_specification with action:'create' for new projects
   - Use manage_specification with action:'update' for existing projects
   - Include comprehensive details: overview, requirements, architecture, success criteria
   - The specification provides context for all features
   - **NEW PROJECTS START IN 'Prototype' STATUS** - they need user approval before processing

5. **CRITICAL: Get User Approval Before Processing**
   - After creating or updating a specification, you MUST ask the user to review and confirm
   - Show them a summary of what will be built
   - Ask explicitly: ""Is this specification correct and complete? Should I approve it for processing?""
   - Only use 'approve_specification' AFTER the user explicitly confirms
   - This changes status from 'Prototype' to 'New', allowing Wyvern to process

## Feature Status Lifecycle:
- **New**: Just created by Dragon, can be updated by Dragon
- **AssignedToWyvern**: Wyvern has taken ownership, Dragon cannot modify (create new feature instead)
- **InProgress**: Being worked on by Kobolds
- **Completed**: Implementation finished

## Project Status Meanings:
- **Prototype**: Specification created but NOT YET APPROVED - Dragon is still refining it with user
- **New**: Specification APPROVED by user, ready for Wyvern assignment
- **WyvernAssigned**: Wyvern is ready to analyze
- **Analyzed**: Tasks have been created from specification
- **SpecificationModified**: Spec was updated, Wyvern will reprocess
- **InProgress**: Kobolds are working on tasks
- **Completed**: All tasks finished
- **Failed**: Error occurred during processing

## Tools Available:
- **list_projects**: List all registered projects with their status and feature counts
- **manage_specification**: Manage specifications (actions: list, load, create, update)
- **manage_feature**: Manage features (actions: list, create, update)
- **approve_specification**: Approve a specification after user confirms (changes Prototype → New)

## Style:
- Be conversational and friendly
- Always check existing projects on first message
- Guide users through the feature workflow
- Be thorough but efficient
- **ALWAYS ask for user confirmation before approving specifications**

Remember: You manage specifications and features. Projects in 'Prototype' status need YOUR approval (after user confirms) before Wyvern can process them.";
        }

        /// <summary>
        /// Starts an interactive session with the user for requirements gathering
        /// </summary>
        /// <param name="initialMessage">Optional initial message from user</param>
        /// <returns>Dragon's response</returns>
        public async Task<string> StartSessionAsync(string? initialMessage = null)
        {
            if (string.IsNullOrEmpty(initialMessage))
            {
                // Default message triggers the welcome behavior
                initialMessage = "Hello, I just connected. Please welcome me and show me my options.";
            }

            // Clear conversation history for new session
            _conversationHistory = new List<Message>();

            var messages = await RunAsync(initialMessage, maxIterations: 5);

            // Store conversation history for future continuations
            _conversationHistory = messages;

            // Return the last assistant message
            var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
            if (lastMessage?.Content == null)
            {
                return "Hello! I'm Dragon 🐉. What project would you like to work on?";
            }

            return ExtractTextFromContent(lastMessage.Content);
        }

        /// <summary>
        /// Continues the conversation with user input
        /// </summary>
        /// <param name="userMessage">User's message</param>
        /// <returns>Dragon's response</returns>
        public async Task<string> ContinueSessionAsync(string userMessage)
        {
            // Continue conversation with full history preserved
            var messages = await ContinueAsync(_conversationHistory, userMessage, maxIterations: 15);

            // Update stored conversation history
            _conversationHistory = messages;

            var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
            if (lastMessage?.Content == null)
            {
                return "I understand. Please continue...";
            }

            return ExtractTextFromContent(lastMessage.Content);
        }

        /// <summary>
        /// Extracts text from message content (handles string, ContentBlock, or List<ContentBlock>)
        /// </summary>
        private string ExtractTextFromContent(object content)
        {
            if (content is string text)
            {
                return text;
            }
            else if (content is ContentBlock block)
            {
                return block.Text ?? "";
            }
            else if (content is IEnumerable<ContentBlock> blocks)
            {
                return string.Join("\n", blocks
                    .Where(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
                    .Select(b => b.Text));
            }

            return content?.ToString() ?? "";
        }

        /// <summary>
        /// Gets the path where specifications are stored
        /// </summary>
        public string SpecificationsPath => _specificationsPath;

        /// <summary>
        /// Gets a specification by name
        /// </summary>
        public Specification? GetSpecification(string name)
        {
            return _specifications.TryGetValue(name, out var spec) ? spec : null;
        }

        /// <summary>
        /// Gets all specifications
        /// </summary>
        public IReadOnlyDictionary<string, Specification> GetAllSpecifications() => _specifications;

        /// <summary>
        /// Gets the number of messages in the current conversation
        /// </summary>
        public int ConversationMessageCount => _conversationHistory.Count;

        /// <summary>
        /// Clears the conversation history (useful for starting fresh without creating new agent)
        /// </summary>
        public void ClearConversationHistory()
        {
            _conversationHistory = new List<Message>();
        }
    }
}
