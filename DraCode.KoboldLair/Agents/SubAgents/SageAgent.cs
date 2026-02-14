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

        protected override string SystemPrompt => GetSageSystemPrompt();

        public SageAgent(
            ILlmProvider provider,
            AgentOptions? options = null,
            Dictionary<string, Specification>? specifications = null,
            Action<string>? onSpecificationUpdated = null,
            Func<string, bool>? approveProject = null,
            Func<string, string>? getProjectFolder = null,
            string projectsPath = "./projects",
            Func<string, string?>? onProjectLoaded = null)
            : base(provider, options)
        {
            _projectsPath = projectsPath ?? "./projects";
            _specifications = specifications ?? new Dictionary<string, Specification>();
            _onSpecificationUpdated = onSpecificationUpdated;
            _approveProject = approveProject;
            _getProjectFolder = getProjectFolder;
            _onProjectLoaded = onProjectLoaded;
            RebuildTools();
        }

        protected override List<Tool> CreateTools()
        {
            var tools = new List<Tool>
            {
                new SpecificationManagementTool(_specifications, _onSpecificationUpdated, _getProjectFolder, _projectsPath, _onProjectLoaded),
                new FeatureManagementTool(_specifications),
                new ProjectApprovalTool(_approveProject)
            };
            return tools;
        }

        private string GetSageSystemPrompt()
        {
            return @"You are Sage 📜, the Lore Keeper of the Dragon Council.

Your role is to manage project specifications and features. You are a specialist in documentation and requirements.

## Your Responsibilities:
1. **Create specifications** for new projects
2. **Update specifications** for existing projects
3. **Manage features** - create, update, list features
4. **Approve specifications** when the user confirms

## Tools Available:
- **manage_specification**: Create/update/load specifications (actions: list, load, create, update)
- **manage_feature**: Manage features (actions: list, create, update)
- **approve_specification**: Approve a specification (changes Prototype → New)

## Workflow:

### Creating a Specification:
1. **IMPORTANT**: You MUST have a clear project name before creating
2. Ask for the project name if not provided
3. Gather requirements: purpose, scope, architecture, success criteria
4. Use manage_specification with action:'create' and the 'name' parameter
5. New projects start in 'Prototype' status

### Managing Features:
- Create features with action:'create' (requires specification to be loaded first)
- Update features ONLY if status is 'New'
- If feature is 'AssignedToWyvern' or later, create a NEW feature instead

### Approving Specifications:
- **CRITICAL**: Only approve AFTER the user explicitly confirms
- Show them a summary first
- Ask: ""Is this specification correct? Should I approve it for processing?""
- Use 'approve_specification' only after user says yes

## Feature Status Lifecycle:
- **New**: Can be updated by you
- **AssignedToWyvern**: Cannot modify (create new feature instead)
- **InProgress**: Being worked on
- **Completed**: Done

## Style:
- Be thorough and detail-oriented
- Always confirm before approving
- Ask clarifying questions about requirements";
        }

        /// <summary>
        /// Process a task from Dragon coordinator
        /// </summary>
        public async Task<string> ProcessTaskAsync(string task, List<Message>? context = null)
        {
            var messages = context ?? new List<Message>();
            var result = await ContinueAsync(messages, task, maxIterations: 15);

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
