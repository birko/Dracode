using DraCode.Agent;
using DraCode.Agent.LLMs.Providers;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Factories
{
    /// <summary>
    /// Factory for creating and managing Drake supervisors.
    /// Each Drake monitors a specific task output path from the Wyvern.
    /// Supports creating Drakes with implementation planning capabilities.
    /// </summary>
    public class DrakeFactory
    {
        private readonly KoboldFactory _koboldFactory;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ProjectConfigurationService _projectConfigService;
        private readonly ProjectRepository? _projectRepository;
        private readonly GitService? _gitService;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly string _projectsPath;
        private readonly bool _planningEnabled;
        private readonly Dictionary<string, Drake> _drakes;
        private readonly Dictionary<string, string?> _drakeProjectIds; // Maps drake name to project ID
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new DrakeFactory
        /// </summary>
        /// <param name="koboldFactory">Kobold factory for creating workers</param>
        /// <param name="providerConfigService">Provider configuration service</param>
        /// <param name="projectConfigService">Project configuration service for parallel limits</param>
        /// <param name="loggerFactory">Optional logger factory for Drake logging</param>
        /// <param name="gitService">Optional git service for committing changes on task completion</param>
        /// <param name="projectsPath">Path to projects directory for plan storage</param>
        /// <param name="planningEnabled">Whether to enable implementation planning (default: true)</param>
        /// <param name="projectRepository">Optional project repository for resolving project paths</param>
        public DrakeFactory(
            KoboldFactory koboldFactory,
            ProviderConfigurationService providerConfigService,
            ProjectConfigurationService projectConfigService,
            ILoggerFactory? loggerFactory = null,
            GitService? gitService = null,
            string projectsPath = "./projects",
            bool planningEnabled = true,
            ProjectRepository? projectRepository = null)
        {
            _koboldFactory = koboldFactory;
            _providerConfigService = providerConfigService;
            _projectConfigService = projectConfigService;
            _projectRepository = projectRepository;
            _loggerFactory = loggerFactory;
            _gitService = gitService;
            _projectsPath = projectsPath;
            _planningEnabled = planningEnabled;
            _drakes = new Dictionary<string, Drake>();
            _drakeProjectIds = new Dictionary<string, string?>();
        }

        /// <summary>
        /// Creates a Drake supervisor that monitors tasks from a specific Wyvern output path
        /// </summary>
        /// <param name="taskFilePath">Path to the Wyvern task markdown file</param>
        /// <param name="drakeName">Optional name for the Drake (uses file path if not specified)</param>
        /// <param name="provider">Optional provider override</param>
        /// <param name="model">Optional model override</param>
        /// <param name="specificationPath">Optional path to project specification</param>
        /// <param name="projectId">Optional project identifier for resource limiting</param>
        /// <returns>Created Drake instance</returns>
        public Drake CreateDrake(
            string taskFilePath,
            string? drakeName = null,
            string? provider = null,
            string? model = null,
            string? specificationPath = null,
            string? projectId = null)
        {
            var name = drakeName ?? taskFilePath;

            // Quick check if Drake already exists (avoids expensive initialization)
            lock (_lock)
            {
                if (_drakes.ContainsKey(name))
                {
                    throw new InvalidOperationException($"Drake with name '{name}' already exists");
                }
            }

            // Perform expensive initialization outside the lock
            // Get provider settings
            string effectiveProvider;
            Dictionary<string, string> config;
            AgentOptions options;

            if (provider != null)
            {
                (effectiveProvider, config, options) = _providerConfigService.GetProviderSettingsForAgent("wyvern");
                effectiveProvider = provider;
                if (model != null)
                {
                    config["model"] = model;
                }
            }
            else
            {
                (effectiveProvider, config, options) = _providerConfigService.GetProviderSettingsForAgent("wyvern");
            }

            // Create task tracker by reading the file if it exists
            var taskTracker = new TaskTracker();
            if (File.Exists(taskFilePath))
            {
                // Load existing tasks from file
                LoadTasksFromFile(taskTracker, taskFilePath);
            }

            // Create logger for Drake
            var logger = _loggerFactory?.CreateLogger<Drake>();

            // Create plan service and planner agent if planning is enabled
            KoboldPlanService? planService = null;
            KoboldPlannerAgent? plannerAgent = null;

            if (_planningEnabled)
            {
                planService = new KoboldPlanService(
                    _projectsPath,
                    _loggerFactory?.CreateLogger<KoboldPlanService>(),
                    _projectRepository
                );

                // Create planner agent using kobold provider settings
                var (plannerProvider, plannerConfig, plannerOptions) =
                    _providerConfigService.GetProviderSettingsForAgent("kobold");

                // Set working directory to project workspace if available
                if (!string.IsNullOrEmpty(projectId))
                {
                    var workspacePath = Path.Combine(_projectsPath, projectId, "workspace");
                    plannerOptions.WorkingDirectory = workspacePath;
                }

                var llmProvider = KoboldLairAgentFactory.CreateLlmProvider(plannerProvider, plannerConfig);
                plannerAgent = new KoboldPlannerAgent(llmProvider, plannerOptions);
            }

            // Create the Drake with all dependencies including plan service
            var drake = new Drake(
                _koboldFactory,
                taskTracker,
                taskFilePath,
                effectiveProvider,
                config,
                options,
                specificationPath,
                projectId,
                logger,
                _providerConfigService,
                _projectConfigService,
                _gitService,
                planService,
                plannerAgent,
                _planningEnabled
            );

            // Lock only for dictionary insertion, with race condition check
            lock (_lock)
            {
                // Double-check in case another thread created it while we were initializing
                if (_drakes.ContainsKey(name))
                {
                    throw new InvalidOperationException($"Drake with name '{name}' already exists (race condition)");
                }

                _drakes[name] = drake;
                _drakeProjectIds[name] = projectId;

                return drake;
            }
        }

        /// <summary>
        /// Gets the count of active drakes for a specific project
        /// </summary>
        public int GetActiveDrakeCountForProject(string? projectId)
        {
            lock (_lock)
            {
                return _drakeProjectIds.Count(kvp => kvp.Value == projectId);
            }
        }

        /// <summary>
        /// Checks if a new drake can be created for the specified project based on the parallel limit
        /// </summary>
        public bool CanCreateDrakeForProject(string? projectId)
        {
            var currentCount = GetActiveDrakeCountForProject(projectId);
            var maxAllowed = _projectConfigService.GetMaxParallelDrakes(projectId ?? string.Empty);
            return currentCount < maxAllowed;
        }

        /// <summary>
        /// Gets an existing Drake by name
        /// </summary>
        public Drake? GetDrake(string drakeName)
        {
            lock (_lock)
            {
                return _drakes.TryGetValue(drakeName, out var drake) ? drake : null;
            }
        }

        /// <summary>
        /// Gets all Drakes
        /// </summary>
        public IReadOnlyCollection<Drake> GetAllDrakes()
        {
            lock (_lock)
            {
                return _drakes.Values.ToList();
            }
        }

        /// <summary>
        /// Removes a Drake
        /// </summary>
        public bool RemoveDrake(string drakeName)
        {
            lock (_lock)
            {
                _drakeProjectIds.Remove(drakeName);
                return _drakes.Remove(drakeName);
            }
        }

        /// <summary>
        /// Gets the total number of Drakes
        /// </summary>
        public int TotalDrakes
        {
            get
            {
                lock (_lock)
                {
                    return _drakes.Count;
                }
            }
        }

        /// <summary>
        /// Loads tasks from a markdown file into the task tracker.
        /// Parses the markdown table format to restore task state after restarts.
        /// </summary>
        private void LoadTasksFromFile(TaskTracker taskTracker, string filePath)
        {
            var tasksLoaded = taskTracker.LoadFromFile(filePath);

            if (tasksLoaded > 0)
            {
                var logger = _loggerFactory?.CreateLogger<DrakeFactory>();
                logger?.LogInformation(
                    "📂 Loaded {Count} task(s) from {FilePath}",
                    tasksLoaded,
                    Path.GetFileName(filePath));
            }
        }
    }
}
