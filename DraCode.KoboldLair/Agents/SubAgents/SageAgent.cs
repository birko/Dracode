using DraCode.Agent;
using DraCode.Agent.Agents;
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
                new DeleteFeatureTool(_specifications),
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
- **delete_feature**: Delete a Draft feature from a specification (requires confirmation)
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

### Specification Completeness Checklist:
Before approving any specification, ensure it covers ALL of the following. If any are missing or unclear, **ask the user** before proceeding:

1. **Tech Stack** — Explicitly state all languages, frameworks, and tools. If the project uses NO frameworks (vanilla), say so explicitly. If there is NO backend or NO database, state that clearly so Wyvern does not invent tasks for them.
2. **Agent Type Hints** — Specify which KoboldLair agent types should be used (e.g., `typescript`, `css`, `html`, `csharp`, `react`, etc.) and which should NOT be used. This prevents Wyvern from assigning wrong agent types.
3. **Architecture Scope** — Is this frontend-only? Backend-only? Fullstack? Wyvern creates work areas (Backend, Frontend, Database, Infrastructure) — clearly state which apply.
4. **Build Tooling** — If the project uses a bundler or build tool (Vite, Webpack, etc.), clarify it is a dev dependency, not a runtime framework. Otherwise the ""no frameworks"" constraint conflicts.
5. **File/Directory Structure** — Propose a structure with source files in subdirectories (not root). Wyvern uses this for task decomposition and file placement.
6. **Task Dependency Order** — Suggest the logical build order (foundation → core logic → UI → integration → testing → docs). This maps to Wyvern's dependency levels.
7. **Out-of-Scope Section** — Explicitly list features that are NOT part of v1 with a clear ""Do NOT Implement"" instruction. Wyvern and Kobolds may otherwise attempt to build them.
8. **Sync/Communication Mechanism** — If the project involves real-time sync between components (e.g., tabs, devices), specify the mechanism (WebSocket, BroadcastChannel, localStorage events, etc.).
9. **Content/Input Loading** — How does data get into the app? File upload, drag-and-drop, URL parameter, API call? Specify the primary method.
10. **Error Handling Strategy** — Define behavior for invalid input, empty data, and edge cases.
11. **Keyboard/Interaction Patterns** — List keyboard shortcuts, gestures, or navigation patterns if applicable.

**When gathering requirements, proactively ask the user about any items from this checklist that are not yet covered.** Frame questions naturally — do not dump the entire checklist at once. Prioritize asking about Tech Stack, Architecture Scope, and Out-of-Scope first, as these have the highest impact on Wyvern's task decomposition.

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
            var text = OrchestratorAgent.ExtractTextFromContent(lastMessage?.Content);
            return string.IsNullOrEmpty(text) ? "Task completed." : text;
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
