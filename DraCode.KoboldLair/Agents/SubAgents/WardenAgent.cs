using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.Agent.Tools;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
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
        private readonly Func<List<RunningAgentInfo>>? _getRunningAgents;
        private readonly Func<string, RunningAgentInfo?>? _getRunningAgentsForProject;
        private readonly Func<KoboldStatistics>? _getGlobalKoboldStats;
        private readonly RetryFailedTaskTool? _retryFailedTaskTool;
        private readonly SetTaskPriorityTool? _setTaskPriorityTool;
        private readonly Func<string, ProjectExecutionState, bool>? _setExecutionState;
        private readonly Func<List<(string Id, string Name, string Status, string? VerificationStatus)>>? _getProjectsNeedingVerification;
        private readonly Func<string, bool>? _retryVerification;
        private readonly Func<string, (bool Success, string? VerificationStatus, DateTime? LastVerified, string? Summary)>? _getVerificationStatus;
        private readonly Func<string, (bool Success, string? Report)>? _getVerificationReport;
        private readonly Func<string, bool>? _skipVerification;

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
            Func<List<(string Id, string Name, string Status, string? ErrorMessage)>>? getFailedProjects = null,
            Func<List<RunningAgentInfo>>? getRunningAgents = null,
            Func<string, RunningAgentInfo?>? getRunningAgentsForProject = null,
            Func<KoboldStatistics>? getGlobalKoboldStats = null,
            RetryFailedTaskTool? retryFailedTaskTool = null,
            SetTaskPriorityTool? setTaskPriorityTool = null,
            Func<string, ProjectExecutionState, bool>? setExecutionState = null,
            Func<List<(string Id, string Name, string Status, string? VerificationStatus)>>? getProjectsNeedingVerification = null,
            Func<string, bool>? retryVerification = null,
            Func<string, (bool Success, string? VerificationStatus, DateTime? LastVerified, string? Summary)>? getVerificationStatus = null,
            Func<string, (bool Success, string? Report)>? getVerificationReport = null,
            Func<string, bool>? skipVerification = null)
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
            _getRunningAgents = getRunningAgents;
            _getRunningAgentsForProject = getRunningAgentsForProject;
            _getGlobalKoboldStats = getGlobalKoboldStats;
            _retryFailedTaskTool = retryFailedTaskTool;
            _setTaskPriorityTool = setTaskPriorityTool;
            _setExecutionState = setExecutionState;
            _getProjectsNeedingVerification = getProjectsNeedingVerification;
            _retryVerification = retryVerification;
            _getVerificationStatus = getVerificationStatus;
            _getVerificationReport = getVerificationReport;
            _skipVerification = skipVerification;
            RebuildTools();
        }

        protected override List<Tool> CreateTools()
        {
            var tools = new List<Tool>
            {
                new AgentConfigurationTool(_getProjectConfig, _getAllProjects, _setAgentEnabled, _setAgentLimit),
                new ExternalPathTool(_getExternalPaths, _addExternalPath, _removeExternalPath, _getAllProjects),
                new RetryAnalysisTool(_getProjectStatus, _retryAnalysis, _getFailedProjects),
                new AgentStatusTool(_getRunningAgents, _getRunningAgentsForProject, _getGlobalKoboldStats),
                new PauseProjectTool(_setExecutionState),
                new ResumeProjectTool(_setExecutionState),
                new SuspendProjectTool(_setExecutionState),
                new CancelProjectTool(_setExecutionState),
                new RetryVerificationTool(_getProjectsNeedingVerification, _retryVerification, _getVerificationStatus),
                new ViewVerificationReportTool(_getVerificationReport),
                new SkipVerificationTool(_skipVerification)
            };
            
            // Add retry failed task tool if available
            if (_retryFailedTaskTool != null)
            {
                tools.Add(_retryFailedTaskTool);
            }
            
            // Add set task priority tool if available
            if (_setTaskPriorityTool != null)
            {
                tools.Add(_setTaskPriorityTool);
            }
            
            return tools;
        }

        private string GetWardenSystemPrompt()
        {
            return @"You are Warden ⚙️, the Agent Overseer of the Dragon Council.

Your role is to manage the workforce - the background agents that process projects (Wyvern, Wyrm, Drake, Kobold).

## Your Responsibilities:
1. **View running agents** - see which Drakes and Kobolds are currently active per project
2. **View agent status** - show enabled/disabled state and limits
3. **Enable/disable agents** - control which agents process a project
4. **Set parallel limits** - control how many agents run concurrently
5. **Manage external path access** - grant/revoke file access outside workspace
6. **Retry failed analysis** - view and retry projects with failed Wyvern analysis
7. **Retry failed tasks** - view and retry individual failed tasks from Kobolds
8. **Control project execution** - pause, resume, suspend, or cancel project execution

## Tools Available:
- **agent_status**: View running agents per project (actions: list, project, summary)
- **manage_agents**: View and manage agent configurations (actions: status, get, enable, disable, set_limit)
- **manage_external_paths**: Control which external paths agents can access (actions: list, add, remove)
- **retry_analysis**: View failed projects and retry Wyvern analysis (actions: list, retry, status)
- **retry_failed_task**: View and retry failed Kobold tasks (actions: list, retry, retry_all)
- **set_task_priority**: Manually override task priority to control execution order
- **pause_project**: Temporarily halt project execution (short-term)
- **resume_project**: Resume paused or suspended project
- **suspend_project**: Long-term hold (awaiting external changes)
- **cancel_project**: Permanently stop project (requires confirmation)

## Agent Types You Oversee:
- **Wyvern**: Analyzes specifications, creates task breakdowns (first step after approval)
- **Wyrm**: Task analysis (if separate from Wyvern)
- **Drake**: Supervises task execution, manages Kobolds
- **Kobold**: Code generation workers that implement tasks

## Workflow:

### Viewing Running Agents:
- Use agent_status with action:'list' to see all projects with running agents
- Use action:'project' with project name to see detailed status for one project
- Use action:'summary' to see global statistics across all projects
- This shows Drakes (supervisors) and Kobolds (workers) currently active
- Includes working duration, task progress, and stuck agent detection

### Viewing Configuration Status:
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

### Controlling Project Execution:
- **pause_project**: Temporarily pause execution during high system load or debugging
  - Use for short-term interruptions
  - Can be resumed at any time
  - Example: ""pause my-project during peak hours""
- **resume_project**: Resume a paused or suspended project
  - Restores normal execution
  - Cannot resume cancelled projects
- **suspend_project**: Long-term hold for projects awaiting external changes
  - Use when project won't continue soon
  - Requires explicit resume action
  - Example: ""suspend project until API keys arrive""
- **cancel_project**: Permanently stop project execution
  - Terminal state - cannot be resumed
  - REQUIRES user confirmation
  - Use when project is abandoned

### Retrying Failed Analysis:
- Use retry_analysis with action:'list' to see all failed projects and their errors
- Use action:'status' with project name to see a specific project's status and full error message
- Use action:'retry' with project name to reset the project and trigger reanalysis
- After retry, the project goes back to 'New' status and Wyvern will pick it up within 60 seconds

### Retrying Failed Tasks:
- Use retry_failed_task with action:'list' to see all failed tasks across all projects
- Failed tasks block project execution until resolved
- Use action:'retry' with task_id to retry a specific failed task
- Use action:'retry_all' with project_id to retry all failed tasks in a project
- After retry, tasks are reset to 'Unassigned' and Drake will pick them up on next cycle

### Setting Task Priority:
- Use set_task_priority to manually override a task's priority
- Priorities: critical (blocking/infrastructure), high (core features), normal (standard), low (polish)
- Higher priority tasks execute first when dependencies allow
- Dependencies always take precedence - can't skip prerequisites
- Use this to accelerate important tasks or defer non-critical work

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
