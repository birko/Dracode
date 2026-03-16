using DraCode.Agent;
using DraCode.Agent.Agents;
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
        private readonly Func<string, string, Task>? _addExternalPath;
        private readonly Func<string, string, Task<bool>>? _removeExternalPath;
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
        private readonly ViewTaskDetailsTool? _viewTaskDetailsTool;
        private readonly ProjectProgressTool? _projectProgressTool;
        private readonly ViewWorkspaceTool? _viewWorkspaceTool;
        private readonly DeleteProjectTool? _deleteProjectTool;
        private readonly NotificationsTool? _notificationsTool;
        private readonly UserSettingsTool? _userSettingsTool;
        private readonly ViewAnalysisTool? _viewAnalysisTool;

        protected override string SystemPrompt => GetWardenSystemPrompt();

        public WardenAgent(
            ILlmProvider provider,
            AgentOptions? options = null,
            Func<string, ProjectAgentConfig?>? getProjectConfig = null,
            Func<List<(string Id, string Name)>>? getAllProjects = null,
            Action<string, string, bool>? setAgentEnabled = null,
            Action<string, string, int>? setAgentLimit = null,
            Func<string, string, Task>? addExternalPath = null,
            Func<string, string, Task<bool>>? removeExternalPath = null,
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
            Func<string, bool>? skipVerification = null,
            ViewTaskDetailsTool? viewTaskDetailsTool = null,
            ProjectProgressTool? projectProgressTool = null,
            ViewWorkspaceTool? viewWorkspaceTool = null,
            DeleteProjectTool? deleteProjectTool = null,
            NotificationsTool? notificationsTool = null,
            UserSettingsTool? userSettingsTool = null,
            ViewAnalysisTool? viewAnalysisTool = null)
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
            _viewTaskDetailsTool = viewTaskDetailsTool;
            _projectProgressTool = projectProgressTool;
            _viewWorkspaceTool = viewWorkspaceTool;
            _deleteProjectTool = deleteProjectTool;
            _notificationsTool = notificationsTool;
            _userSettingsTool = userSettingsTool;
            _viewAnalysisTool = viewAnalysisTool;
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
            
            // Add optional tools if available
            if (_retryFailedTaskTool != null)
                tools.Add(_retryFailedTaskTool);
            if (_setTaskPriorityTool != null)
                tools.Add(_setTaskPriorityTool);
            if (_viewTaskDetailsTool != null)
                tools.Add(_viewTaskDetailsTool);
            if (_projectProgressTool != null)
                tools.Add(_projectProgressTool);
            if (_viewWorkspaceTool != null)
                tools.Add(_viewWorkspaceTool);
            if (_deleteProjectTool != null)
                tools.Add(_deleteProjectTool);
            if (_notificationsTool != null)
                tools.Add(_notificationsTool);
            if (_userSettingsTool != null)
                tools.Add(_userSettingsTool);
            if (_viewAnalysisTool != null)
                tools.Add(_viewAnalysisTool);

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
9. **View notifications** - check feature completion, project events, escalation alerts
10. **Manage global settings** - view/change LLM provider assignments for all agent types
11. **View analysis results** - inspect what Wyrm and Wyvern decided about a project

## Tools Available:
- **agent_status**: View running agents per project (actions: list, project, summary)
- **manage_agents**: View and manage per-project agent configurations (actions: status, get, enable, disable, set_limit)
- **manage_external_paths**: Control which external paths agents can access (actions: list, add, remove)
- **retry_analysis**: View failed projects and retry Wyvern analysis (actions: list, retry, status)
- **retry_failed_task**: View and retry failed Kobold tasks (actions: list, retry, retry_all)
- **set_task_priority**: Manually override task priority to control execution order
- **view_task_details**: View detailed task info including errors, plan steps, dependencies (actions: list, detail, plan)
- **project_progress**: View project progress analytics - completion %, breakdowns, success rates (actions: overview, all)
- **view_workspace**: Browse generated output files in workspace (actions: tree, stats, recent)
- **delete_project**: Permanently remove a cancelled project from registry
- **pause_project**: Temporarily halt project execution (short-term)
- **resume_project**: Resume paused or suspended project
- **suspend_project**: Long-term hold (awaiting external changes)
- **cancel_project**: Permanently stop project (requires confirmation)
- **view_notifications**: View and dismiss project notifications - feature completions, alerts, errors (actions: list, project, dismiss, dismiss_all)
- **user_settings**: View and change global LLM provider settings for all agent types (actions: view, set_provider, set_kobold_type)
- **view_analysis**: View Wyrm and Wyvern analysis results for a project (actions: wyrm, wyvern, summary)

## Agent Types You Oversee:
- **Wyrm**: Pre-analyzes specifications, recommends languages/agent types/tech stack. Also used by Drake to select specialist Kobold types for each task.
- **Wyvern**: Detailed analysis of specs (guided by Wyrm recommendations), creates task breakdowns with priorities and dependencies.
- **Drake**: Supervises task execution, creates implementation plans, summons and monitors Kobolds.
- **Kobold**: Code generation workers that implement tasks step-by-step from plans.

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
- New projects have all agents enabled by default

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
1. Wyrm pre-analyzes spec → recommends languages, agent types, tech stack (every 60s check)
2. Wyvern analyzes spec (guided by Wyrm) → creates tasks with priorities and dependencies (every 60s check)
3. Drake picks up tasks → creates implementation plans → summons Kobolds (every 30s check)
4. Kobolds execute plans step-by-step → output code to workspace
5. Verification service checks build/tests → marks project Completed or creates fix tasks

## Style:
- Be authoritative but helpful
- Explain what each setting does
- Warn about implications of changes (especially for external path access - it's a security consideration)
- Suggest optimal configurations
- When users ask about failed projects or errors, proactively offer to show the error details and retry";
        }

        /// <summary>
        /// Process a task from Dragon coordinator with latency tracking
        /// </summary>
        public async Task<string> ProcessTaskAsync(string task, List<Message>? context = null)
        {
            var startTime = DateTime.UtcNow;
            SendMessage("debug", "[Warden] START | Task: " + (task.Length > 80 ? task.Substring(0, 80) + "..." : task));

            var messages = context ?? new List<Message>();
            var result = await ContinueAsync(messages, task, maxIterations: 10);

            var duration = DateTime.UtcNow - startTime;
            SendMessage("debug", $"[Warden] COMPLETE | Duration: {duration.TotalMilliseconds:F0}ms");

            var lastMessage = result.LastOrDefault(m => m.Role == "assistant");
            var text = OrchestratorAgent.ExtractTextFromContent(lastMessage?.Content);
            return string.IsNullOrEmpty(text) ? "Task completed." : text;
        }
    }
}
