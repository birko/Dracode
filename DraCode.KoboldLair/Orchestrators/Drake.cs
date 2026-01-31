using DraCode.Agent;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Orchestrators
{
    /// <summary>
    /// Drake is a supervisor that manages the lifecycle of Kobolds based on task status.
    /// It monitors tasks, creates Kobolds for execution, and updates task status based on Kobold progress.
    /// </summary>
    public class Drake
    {
        private readonly KoboldFactory _koboldFactory;
        private readonly TaskTracker _taskTracker;
        private readonly string _outputMarkdownPath;
        private readonly string _defaultProvider;
        private readonly Dictionary<string, string>? _defaultConfig;
        private readonly AgentOptions? _defaultOptions;
        private readonly string? _specificationPath;
        private readonly string? _projectId;
        private readonly ILogger<Drake>? _logger;
        private readonly ProviderConfigurationService? _providerConfigService;
        private Wyvern? _wyvern;

        /// <summary>
        /// Maps TaskId to KoboldId for tracking active assignments
        /// </summary>
        private readonly Dictionary<string, Guid> _taskToKoboldMap;

        /// <summary>
        /// Creates a new Drake supervisor
        /// </summary>
        /// <param name="koboldFactory">Factory for managing Kobolds</param>
        /// <param name="taskTracker">Tracker for managing tasks</param>
        /// <param name="outputMarkdownPath">Path to markdown output file</param>
        /// <param name="defaultProvider">Default LLM provider for Kobolds</param>
        /// <param name="defaultConfig">Default provider configuration</param>
        /// <param name="defaultOptions">Default agent options</param>
        /// <param name="specificationPath">Optional path to project specification</param>
        /// <param name="projectId">Optional project identifier for resource limiting</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <param name="providerConfigService">Optional provider configuration service for agent-type-specific providers</param>
        public Drake(
            KoboldFactory koboldFactory,
            TaskTracker taskTracker,
            string outputMarkdownPath,
            string defaultProvider = "openai",
            Dictionary<string, string>? defaultConfig = null,
            AgentOptions? defaultOptions = null,
            string? specificationPath = null,
            string? projectId = null,
            ILogger<Drake>? logger = null,
            ProviderConfigurationService? providerConfigService = null)
        {
            _koboldFactory = koboldFactory;
            _taskTracker = taskTracker;
            _outputMarkdownPath = outputMarkdownPath;
            _defaultProvider = defaultProvider;
            _defaultConfig = defaultConfig;
            _defaultOptions = defaultOptions;
            _specificationPath = specificationPath;
            _projectId = projectId;
            _logger = logger;
            _providerConfigService = providerConfigService;
            _taskToKoboldMap = new Dictionary<string, Guid>();
        }

        /// <summary>
        /// Sets the Wyvern for this Drake to enable feature status updates
        /// </summary>
        public void SetWyvern(Wyvern wyvern)
        {
            _wyvern = wyvern;
        }

        /// <summary>
        /// Summons (creates) a Kobold for a specific task
        /// </summary>
        /// <param name="task">Task to assign</param>
        /// <param name="agentType">Type of agent to create</param>
        /// <param name="provider">LLM provider (optional, resolves by agent type if not specified)</param>
        /// <returns>The summoned Kobold, or null if resource limit reached</returns>
        public Kobold? SummonKobold(TaskRecord task, string agentType, string? provider = null)
        {
            // Resolve provider: explicit override > agent-type-specific > global default
            var effectiveProvider = provider
                ?? _providerConfigService?.GetProviderForKoboldAgentType(agentType)
                ?? _defaultProvider;
            var projectId = task.ProjectId ?? _projectId;

            // Check if we can create a kobold for this project (resource limit check)
            if (!_koboldFactory.CanCreateKoboldForProject(projectId))
            {
                _logger?.LogDebug(
                    "Cannot summon kobold for task {TaskId} - project {ProjectId} has reached its parallel kobold limit",
                    task.Id, projectId ?? "(default)");
                return null;
            }

            // Create the Kobold
            var kobold = _koboldFactory.CreateKobold(
                effectiveProvider,
                agentType,
                _defaultOptions,
                _defaultConfig
            );

            // Load specification context if available
            string? specificationContext = null;
            if (!string.IsNullOrEmpty(_specificationPath) && File.Exists(_specificationPath))
            {
                try
                {
                    specificationContext = File.ReadAllText(_specificationPath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not load specification from {Path}", _specificationPath);
                }
            }

            // Assign the task with description, project, and specification context
            kobold.AssignTask(Guid.Parse(task.Id), task.Task, projectId, specificationContext);

            // Track the mapping
            _taskToKoboldMap[task.Id] = kobold.Id;

            // Update task status
            _taskTracker.UpdateTask(task, TaskStatus.NotInitialized, agentType);
            SaveTasksToFile();

            return kobold;
        }

        /// <summary>
        /// Unsummons (destroys) a Kobold and removes it from the factory
        /// </summary>
        /// <param name="koboldId">ID of the Kobold to unsummon</param>
        /// <returns>True if unsummoned successfully</returns>
        public bool UnsummonKobold(Guid koboldId)
        {
            var kobold = _koboldFactory.GetKobold(koboldId);
            if (kobold == null)
                return false;

            // Remove from task mapping
            if (kobold.TaskId.HasValue)
            {
                var taskIdString = kobold.TaskId.Value.ToString();
                _taskToKoboldMap.Remove(taskIdString);
            }

            // Remove from factory
            return _koboldFactory.RemoveKobold(koboldId);
        }

        /// <summary>
        /// Starts a Kobold working on its assigned task (executes the task automatically).
        /// The Kobold manages its own state transitions during execution.
        /// </summary>
        /// <param name="koboldId">ID of the Kobold to start</param>
        /// <param name="maxIterations">Maximum iterations for agent execution</param>
        /// <returns>Messages from agent execution</returns>
        public async Task<List<Message>> StartKoboldWorkAsync(Guid koboldId, int maxIterations = 30)
        {
            var kobold = _koboldFactory.GetKobold(koboldId);
            if (kobold == null)
                throw new InvalidOperationException($"Kobold {koboldId} not found");

            if (!kobold.TaskId.HasValue)
                throw new InvalidOperationException($"Kobold {koboldId} has no assigned task");

            // Update task status to Working (informational only - Kobold controls its own state)
            var task = _taskTracker.GetTaskById(kobold.TaskId.Value.ToString());
            if (task != null)
            {
                _taskTracker.UpdateTask(task, TaskStatus.Working);
                SaveTasksToFile();
            }

            // Start the Kobold (this executes the task and manages state automatically)
            var messages = await kobold.StartWorkingAsync(maxIterations);

            // Sync task status from Kobold's final state
            SyncTaskFromKobold(kobold);

            return messages;
        }

        /// <summary>
        /// Syncs task status from Kobold's current state.
        /// Drake observes Kobold state but does not forcefully change it.
        /// </summary>
        /// <param name="kobold">Kobold to sync from</param>
        private void SyncTaskFromKobold(Kobold kobold)
        {
            if (!kobold.TaskId.HasValue)
                return;

            var task = _taskTracker.GetTaskById(kobold.TaskId.Value.ToString());
            if (task == null)
                return;

            // Map Kobold status to Task status
            var taskStatus = kobold.Status switch
            {
                KoboldStatus.Unassigned => TaskStatus.Unassigned,
                KoboldStatus.Assigned => TaskStatus.NotInitialized,
                KoboldStatus.Working => TaskStatus.Working,
                KoboldStatus.Done => TaskStatus.Done,
                _ => task.Status // Keep current if unknown
            };

            // Update task with Kobold's error if present
            if (kobold.HasError)
            {
                _taskTracker.SetError(task, kobold.ErrorMessage ?? "Unknown error");
            }

            _taskTracker.UpdateTask(task, taskStatus);
            SaveTasksToFile();
        }

        /// <summary>
        /// Executes a task using a Kobold (summon, work, and automatically sync status).
        /// The Kobold manages its own state - Drake only observes and syncs to TaskTracker.
        /// </summary>
        /// <param name="task">Task to execute</param>
        /// <param name="agentType">Type of agent to use</param>
        /// <param name="maxIterations">Maximum iterations for agent</param>
        /// <param name="provider">LLM provider (optional)</param>
        /// <param name="messageCallback">Callback for progress messages</param>
        /// <returns>Task result and associated Kobold, or null if resource limit reached</returns>
        public async Task<(List<Message> messages, Kobold kobold)?> ExecuteTaskAsync(
            TaskRecord task,
            string agentType,
            int maxIterations = 30,
            string? provider = null,
            Action<string, string>? messageCallback = null)
        {
            // Summon Kobold (may return null if resource limit reached)
            var kobold = SummonKobold(task, agentType, provider);

            if (kobold == null)
            {
                // Resource limit reached - task remains in queue for retry on next cycle
                var projectId = task.ProjectId ?? _projectId ?? "(default)";
                messageCallback?.Invoke("info", $"⏸️ Drake cannot summon kobold for task {task.Id.ToString()[..8]} - project {projectId} at parallel limit. Will retry.");
                return null;
            }

            messageCallback?.Invoke("info", $"🐉 Drake summoned Kobold {kobold.Id.ToString()[..8]} for task {task.Id.ToString()[..8]}");

            List<Message> messages;

            try
            {
                // Set message callback on agent
                if (messageCallback != null)
                {
                    kobold.Agent.SetMessageCallback(messageCallback);
                }

                // Start work (Kobold automatically manages its state transitions)
                messageCallback?.Invoke("info", $"⚡ Kobold {kobold.Id.ToString()[..8]} started working on: {kobold.TaskDescription}");
                messages = await StartKoboldWorkAsync(kobold.Id, maxIterations);

                // Check Kobold's final state
                if (kobold.IsSuccess)
                {
                    messageCallback?.Invoke("success", $"✓ Kobold {kobold.Id.ToString()[..8]} completed task successfully");
                }
                else if (kobold.HasError)
                {
                    messageCallback?.Invoke("error", $"✗ Kobold {kobold.Id.ToString()[..8]} failed: {kobold.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                messageCallback?.Invoke("error", $"✗ Kobold {kobold.Id.ToString()[..8]} execution error: {ex.Message}");
                messages = new List<Message>();

                // Sync status from Kobold (it should have captured the error)
                SyncTaskFromKobold(kobold);
                throw;
            }

            // Final sync of task status from Kobold
            SyncTaskFromKobold(kobold);

            // Update feature status if Wyvern is available
            UpdateFeatureStatus();

            return (messages, kobold);
        }

        /// <summary>
        /// Monitors all tasks and syncs their status from associated Kobolds.
        /// Drake observes Kobold states but does not force state changes.
        /// Also updates feature status through Wyvern if available.
        /// </summary>
        public void MonitorTasks()
        {
            var allTasks = _taskTracker.GetAllTasks();

            foreach (var task in allTasks)
            {
                // Check if task has an associated Kobold
                if (_taskToKoboldMap.TryGetValue(task.Id, out var koboldId))
                {
                    var kobold = _koboldFactory.GetKobold(koboldId);
                    if (kobold != null)
                    {
                        // Sync task status from Kobold's current state
                        SyncTaskFromKobold(kobold);
                    }
                    else
                    {
                        // Kobold was removed but task still mapped - clean up
                        _taskToKoboldMap.Remove(task.Id);
                    }
                }
            }

            SaveTasksToFile();

            // Update feature status if Wyrm is available
            UpdateFeatureStatus();
        }

        /// <summary>
        /// Updates feature status based on current task statuses
        /// </summary>
        private void UpdateFeatureStatus()
        {
            if (_wyvern == null)
                return;

            var allTasks = _taskTracker.GetAllTasks();
            var taskStatuses = allTasks.ToDictionary(t => t.Id, t => t.Status);

            _wyvern.UpdateFeatureStatus(taskStatuses);
        }

        /// <summary>
        /// Unsummons all completed Kobolds
        /// </summary>
        /// <returns>Number of Kobolds unsummoned</returns>
        public int UnsummonCompletedKobolds()
        {
            var completedKobolds = _koboldFactory.GetKoboldsByStatus(KoboldStatus.Done);
            int count = 0;

            foreach (var kobold in completedKobolds)
            {
                if (UnsummonKobold(kobold.Id))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Gets statistics about the Drake's managed Kobolds
        /// </summary>
        public DrakeStatistics GetStatistics()
        {
            var koboldStats = _koboldFactory.GetStatistics();
            var allTasks = _taskTracker.GetAllTasks().ToList();

            return new DrakeStatistics
            {
                TotalKobolds = koboldStats.Total,
                UnassignedKobolds = koboldStats.Unassigned,
                AssignedKobolds = koboldStats.Assigned,
                WorkingKobolds = koboldStats.Working,
                DoneKobolds = koboldStats.Done,
                TotalTasks = allTasks.Count,
                UnassignedTasks = allTasks.Count(t => t.Status == TaskStatus.Unassigned),
                WorkingTasks = allTasks.Count(t => t.Status == TaskStatus.Working),
                DoneTasks = allTasks.Count(t => t.Status == TaskStatus.Done),
                ActiveAssignments = _taskToKoboldMap.Count
            };
        }

        /// <summary>
        /// Gets the Kobold assigned to a specific task
        /// </summary>
        public Kobold? GetKoboldForTask(string taskId)
        {
            if (_taskToKoboldMap.TryGetValue(taskId, out var koboldId))
            {
                return _koboldFactory.GetKobold(koboldId);
            }
            return null;
        }

        /// <summary>
        /// Saves the current task state to the markdown file
        /// </summary>
        private void SaveTasksToFile()
        {
            var markdown = _taskTracker.GenerateMarkdown("Drake Task Report");
            File.WriteAllText(_outputMarkdownPath, markdown);
        }

        /// <summary>
        /// Forces a save of the current task state
        /// </summary>
        public void UpdateTasksFile()
        {
            SaveTasksToFile();
        }
    }
}
