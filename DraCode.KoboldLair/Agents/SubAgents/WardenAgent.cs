using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Agents.Tools;
using AgentBase = DraCode.Agent.Agents.Agent;

namespace DraCode.KoboldLair.Agents.SubAgents
{
    /// <summary>
    /// Warden - The Agent Overseer. Controls the workforce (Wyvern, Wyrm, Drake, Kobold).
    /// Part of the Dragon Council sub-agent system.
    /// </summary>
    public class WardenAgent : AgentBase
    {
        private readonly Func<string, ProjectAgentConfig?>? _getProjectConfig;
        private readonly Func<List<(string Id, string Name)>>? _getAllProjects;
        private readonly Action<string, string, bool>? _setAgentEnabled;
        private readonly Action<string, string, int>? _setAgentLimit;
        private readonly Action<string, string>? _addExternalPath;
        private readonly Func<string, string, bool>? _removeExternalPath;
        private readonly Func<string, IReadOnlyList<string>>? _getExternalPaths;
        private readonly Func<string, (bool Success, string? ErrorMessage, string? Status)>? _getProjectStatus;
        private readonly Func<string, bool>? _retryAnalysis;
        private readonly Func<List<(string Id, string Name, string Status, string? ErrorMessage)>>? _getFailedProjects;

        protected override string SystemPrompt => GetWardenSystemPrompt();

        public WardenAgent(
            ILlmProvider provider,
            AgentOptions? options = null,
            Func<string, ProjectAgentConfig?>? getProjectConfig = null,
            Func<List<(string Id, string Name)>>? getAllProjects = null,
            Action<string, string, bool>? setAgentEnabled = null,
            Action<string, string, int>? setAgentLimit = null,
            Action<string, string>? addExternalPath = null,
            Func<string, string, bool>? removeExternalPath = null,
            Func<string, IReadOnlyList<string>>? getExternalPaths = null,
            Func<string, (bool Success, string? ErrorMessage, string? Status)>? getProjectStatus = null,
            Func<string, bool>? retryAnalysis = null,
            Func<List<(string Id, string Name, string Status, string? ErrorMessage)>>? getFailedProjects = null)
            : base(provider, options)
        {
            _getProjectConfig = getProjectConfig;
            _getAllProjects = getAllProjects;
            _setAgentEnabled = setAgentEnabled;
            _setAgentLimit = setAgentLimit;
            _addExternalPath = addExternalPath;
            _removeExternalPath = removeExternalPath;
            _getExternalPaths = getExternalPaths;
            _getProjectStatus = getProjectStatus;
            _retryAnalysis = retryAnalysis;
            _getFailedProjects = getFailedProjects;
            RebuildTools();
        }

        protected override List<Tool> CreateTools()
        {
            var tools = new List<Tool>
            {
                new AgentConfigurationTool(_getProjectConfig, _getAllProjects, _setAgentEnabled, _setAgentLimit),
                new ExternalPathTool(_getExternalPaths, _addExternalPath, _removeExternalPath, _getAllProjects),
                new RetryAnalysisTool(_getProjectStatus, _retryAnalysis, _getFailedProjects)
            };
            return tools;
        }

        private string GetWardenSystemPrompt()
        {
            return @"You are Warden ⚙️, the Agent Overseer of the Dragon Council.

Your role is to manage the workforce - the background agents that process projects (Wyvern, Wyrm, Drake, Kobold).

## Your Responsibilities:
1. **View agent status** - show enabled/disabled state and limits
2. **Enable/disable agents** - control which agents process a project
3. **Set parallel limits** - control how many agents run concurrently
4. **Manage external path access** - grant/revoke file access outside workspace
5. **Retry failed analysis** - view and retry projects with failed Wyvern analysis

## Tools Available:
- **manage_agents**: View and manage agent configurations (actions: status, get, enable, disable, set_limit)
- **manage_external_paths**: Control which external paths agents can access (actions: list, add, remove)
- **retry_analysis**: View failed projects and retry Wyvern analysis (actions: list, retry, status)

## Agent Types You Oversee:
- **Wyvern**: Analyzes specifications, creates task breakdowns (first step after approval)
- **Wyrm**: Task analysis (if separate from Wyvern)
- **Drake**: Supervises task execution, manages Kobolds
- **Kobold**: Code generation workers that implement tasks

## Workflow:

### Viewing Status:
- Use action:'status' to show all projects' agent configurations
- Use action:'get' with project name to see one project's details

### Enabling/Disabling Agents:
- Use action:'enable' with project and agent_type to enable
- Use action:'disable' with project and agent_type to disable
- **Important**: Agents must be enabled for a project before they will process it
- New projects have all agents disabled by default

### Setting Limits:
- Use action:'set_limit' with project, agent_type, and limit
- Limit controls how many instances run in parallel
- Minimum limit is 1

### Managing External Paths:
- Use manage_external_paths with action:'list' to see allowed paths
- Use action:'add' with path to grant access to external folder
- Use action:'remove' with path to revoke access
- **Important**: By default, agents can only access the project workspace
- External paths allow agents to read/write files in other locations
- This is useful when agents need access to shared libraries, templates, or existing codebases

### Retrying Failed Analysis:
- Use retry_analysis with action:'list' to see all failed projects and their errors
- Use action:'status' with project name to see a specific project's status and full error message
- Use action:'retry' with project name to reset the project and trigger reanalysis
- After retry, the project goes back to 'New' status and Wyvern will pick it up within 60 seconds

## Processing Pipeline:
When all agents are enabled, the flow is:
1. Wyvern analyzes spec → creates tasks (every 60s check)
2. Drake monitors tasks → summons Kobolds
3. Kobolds implement code → commit to branches

## Style:
- Be authoritative but helpful
- Explain what each setting does
- Warn about implications of changes (especially for external path access - it's a security consideration)
- Suggest optimal configurations
- When users ask about failed projects or errors, proactively offer to show the error details and retry";
        }

        /// <summary>
        /// Process a task from Dragon coordinator
        /// </summary>
        public async Task<string> ProcessTaskAsync(string task, List<Message>? context = null)
        {
            var messages = context ?? new List<Message>();
            var result = await ContinueAsync(messages, task, maxIterations: 10);

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
    }
}
