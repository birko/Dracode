using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Projects;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Agents.SubAgents
{
    /// <summary>
    /// Sage - The Lore Keeper. Handles specifications and features.
    /// Part of the Dragon Council sub-agent system.
    /// </summary>
    public class SageAgent : AgentBase
    {
        private readonly string _projectsPath;
        private readonly Dictionary<string, Specification> _specifications;
        private readonly Action<string>? _onSpecificationUpdated;
        private readonly Func<string, bool>? _approveProject;
        private readonly Func<string, string>? _getProjectFolder;
        private readonly Func<string, string?>? _onProjectLoaded;
        private readonly Func<string?>? _getActiveProjectName;

        protected override string SystemPrompt => GetSageSystemPrompt();

        public SageAgent(
            ILlmProvider provider,
            AgentOptions? options = null,
            Dictionary<string, Specification>? specifications = null,
            Action<string>? onSpecificationUpdated = null,
            Func<string, bool>? approveProject = null,
            Func<string, string>? getProjectFolder = null,
            string projectsPath = "./projects",
            Func<string, string?>? onProjectLoaded = null,
            Func<string?>? getActiveProjectName = null)
            : base(provider, options)
        {
            _projectsPath = projectsPath ?? "./projects";
            _specifications = specifications ?? new Dictionary<string, Specification>();
            _onSpecificationUpdated = onSpecificationUpdated;
            _approveProject = approveProject;
            _getProjectFolder = getProjectFolder;
            _onProjectLoaded = onProjectLoaded;
            _getActiveProjectName = getActiveProjectName;
            RebuildTools();
        }

        protected override List<Tool> CreateTools()
        {
            var tools = new List<Tool>
            {
                new SpecificationManagementTool(_specifications, _onSpecificationUpdated, _getProjectFolder, _projectsPath, _onProjectLoaded),
                new FeatureManagementTool(_specifications),
                new ProcessFeaturesTool(_specifications, _onSpecificationUpdated),
                new SpecificationHistoryTool(_specifications),
                new ProjectApprovalTool(_approveProject)
            };
            return tools;
        }

        private string GetSageSystemPrompt()
        {
            var prompt = @"You are Sage 📜, the Lore Keeper of the Dragon Council.

Your role is to manage project specifications and features. You are a specialist in documentation and requirements.

## Your Responsibilities:
1. **Create specifications** for new projects
2. **Update specifications** for existing projects
3. **Manage features** - create, update, list features
4. **Process features** - promote draft features to ready, trigger analysis
5. **Approve specifications** when the user confirms

## Tools Available:
- **manage_specification**: Create/update/load specifications (actions: list, load, create, update)
- **manage_feature**: Manage features (actions: list, create, update)
- **process_features**: Manage feature processing workflow (actions: list, promote, update_spec)
- **view_specification_history**: View specification version history
- **approve_specification**: Approve a specification (changes Prototype → New)

## Workflow:

### Creating a Specification:
1. **IMPORTANT**: You MUST have a clear project name before creating
2. Ask for the project name if not provided
3. Gather requirements: purpose, scope, architecture, success criteria
4. Use manage_specification with action:'create' and the 'name' parameter
5. New projects start in 'Prototype' status

### Managing Features (Draft → Ready → Processed):
1. **Create** features with `manage_feature` action:'create' → Features start as **Draft** 📝
2. **List** draft features with `process_features` action:'list' → See all pending drafts
3. **Promote** to Ready with `process_features` action:'promote' + feature_names → Mark as **Ready** ✅
4. **Trigger** analysis with `process_features` action:'update_spec' → Wyvern processes Ready features
5. Features can only be updated while in **Draft** status

### Feature Status Lifecycle:
- **Draft** 📝: Newly created, can be modified, NOT processed by Wyvern
- **Ready** ✅: Marked for processing, will be picked up by Wyvern
- **AssignedToWyvern** 📋: Wyvern is creating tasks
- **InProgress** 🔨: Being worked on by Kobolds
- **Completed** 🎉: Done

### Approving Specifications:
- **CRITICAL**: Only approve AFTER the user explicitly confirms
- Show them a summary first
- Ask: ""Is this specification correct? Should I approve it for processing?""
- Use 'approve_specification' only after user says yes

## Style:
- Be thorough and detail-oriented
- Always confirm before approving
- Ask clarifying questions about requirements
- When users add features, remind them to use `process_features` to promote and trigger analysis";

            // Add active project context if available
            var activeProjectName = _getActiveProjectName?.Invoke();
            if (!string.IsNullOrEmpty(activeProjectName))
            {
                prompt += $@"

## ACTIVE PROJECT CONTEXT
There is an active project: **{activeProjectName}**
When creating a specification for this project, you MUST use the exact name ""{activeProjectName}"" as the project name.
Do NOT invent a new name or variation - use the existing project name exactly as shown above.";
            }

            return prompt;
        }

        /// <summary>
        /// Process a task from Dragon coordinator with latency tracking
        /// </summary>
        public async Task<string> ProcessTaskAsync(string task, List<Message>? context = null)
        {
            var startTime = DateTime.UtcNow;
            SendMessage("debug", "[Sage] START | Task: " + (task.Length > 80 ? task.Substring(0, 80) + "..." : task));

            var messages = context ?? new List<Message>();
            var result = await ContinueAsync(messages, task, maxIterations: 15);

            var duration = DateTime.UtcNow - startTime;
            SendMessage("debug", $"[Sage] COMPLETE | Duration: {duration.TotalMilliseconds:F0}ms");

            var lastMessage = result.LastOrDefault(m => m.Role == "assistant");
            return ExtractTextFromContent(lastMessage?.Content);
        }

        private string ExtractTextFromContent(object? content)
        {
            if (content == null) return "Task completed.";
            if (content is string text) return text;
            if (content is ContentBlock block) return block.Text ?? "";
            if (content is IEnumerable<ContentBlock> blocks)
            {
                return string.Join("\n", blocks
                    .Where(b => b.Type == "text" && !string.IsNullOrEmpty(b.Text))
                    .Select(b => b.Text));
            }
            return content.ToString() ?? "";
        }

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
    }
}
