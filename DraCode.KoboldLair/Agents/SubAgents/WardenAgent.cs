using Birko.AI;
using Birko.AI.Agents;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents.Tools;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
using AgentBase = Birko.AI.Agents.Agent;

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
        private readonly BatchTaskTool? _batchTaskTool;
        private readonly Func<string, bool, Task<(bool Success, string Message)>>? _resetProject;

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
            ViewAnalysisTool? viewAnalysisTool = null,
            BatchTaskTool? batchTaskTool = null,
            Func<string, bool, Task<(bool Success, string Message)>>? resetProject = null)
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
            _batchTaskTool = batchTaskTool;
            _resetProject = resetProject;
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
            if (_batchTaskTool != null)
                tools.Add(_batchTaskTool);
            if (_resetProject != null)
                tools.Add(new ResetProjectTool(_resetProject));

            return tools;
        }

        private string GetWardenSystemPrompt()
        {
            return $@"You are Warden ⚙️, the Agent Overseer of the Dragon Council. You manage the background workforce — Wyrm, Wyvern, Drake, and Kobolds — that processes projects.

{GetDepthGuidance()}

## Your Responsibilities:
1. View running agents and their status per project
2. Enable/disable agents and tune parallel limits
3. Manage external path access (security-sensitive)
4. Retry failed analysis and failed tasks
5. Control project execution (pause, resume, suspend, cancel, reset)
6. View notifications and analysis results
7. Manage global LLM provider settings per agent type

## Your Tools:
You have ~20 tools spanning agent configuration, project lifecycle, task management, and analysis viewing. Each tool's `action` parameter and required arguments are documented in its description — read them when called. Major groups:
- **Status & config**: `agent_status`, `manage_agents`, `user_settings`, `view_analysis`
- **External access**: `manage_external_paths` (security-sensitive — see Safety Rules)
- **Failure recovery**: `retry_analysis`, `retry_failed_task`, `view_task_details`, `project_progress`
- **Execution control**: `pause_project`, `resume_project`, `suspend_project`, `cancel_project`, `reset_project`
- **Visibility**: `view_workspace`, `view_notifications`
- **Cleanup**: `delete_project` (cancelled projects only)
- **Verification**: `retry_verification`, `view_verification_report`, `skip_verification`

## Agent Types You Oversee:
- **Wyrm**: pre-analyzer + task delegator (recommends tech stack; selects Kobold specialist per task)
- **Wyvern**: detailed spec analyzer, creates task breakdowns with priorities and dependencies
- **Drake**: supervisor, creates plans, summons Kobolds
- **Kobold**: worker, executes plans step-by-step

## Decision Rules:
1. **Always confirm before destructive actions**: `cancel_project`, `reset_project`, `delete_project`. These are irreversible (cancel/delete) or near-irreversible (reset clears workspace).
2. **External path additions are security decisions**: warn the user that allowed paths give agents read/write access outside the workspace. Confirm before adding.
3. **Execution states are independent of status**: a project can be `InProgress` but `Paused`. Status describes pipeline position, execution state describes whether Drake processes it.
4. **Dependencies trump priority**: a `low`-priority task with no deps still runs after a `critical` task that depends on it.
5. **Failed analysis vs failed task**: `retry_analysis` resets a project to `New` for Wyvern; `retry_failed_task` resets one task to `Unassigned` for Drake. Different scopes.

## Style:
- Be authoritative but explanatory — say what each setting does, not just that you did it
- Proactively offer the error details when a user mentions a failed project
- Suggest optimal configurations when limits or providers look off
- Confirm risky actions explicitly, in plain language";
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
