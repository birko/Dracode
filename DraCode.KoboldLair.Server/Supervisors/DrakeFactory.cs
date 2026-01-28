using DraCode.Agent;
using DraCode.KoboldLair.Server.Agents.Wyvern;
using DraCode.KoboldLair.Server.Agents.Kobold;
using DraCode.KoboldLair.Server.Supervisors;
using DraCode.KoboldLair.Server.Services;

namespace DraCode.KoboldLair.Server.Supervisors
{
    /// <summary>
    /// Factory for creating and managing Drake supervisors.
    /// Each Drake monitors a specific task output path from the Wyvern.
    /// </summary>
    public class DrakeFactory
    {
        private readonly KoboldFactory _koboldFactory;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly Dictionary<string, Drake> _drakes;
        private readonly object _lock = new object();

        /// <summary>
        /// Creates a new DrakeFactory
        /// </summary>
        /// <param name="koboldFactory">Kobold factory for creating workers</param>
        /// <param name="providerConfigService">Provider configuration service</param>
        /// <param name="loggerFactory">Optional logger factory for Drake logging</param>
        public DrakeFactory(
            KoboldFactory koboldFactory,
            ProviderConfigurationService providerConfigService,
            ILoggerFactory? loggerFactory = null)
        {
            _koboldFactory = koboldFactory;
            _providerConfigService = providerConfigService;
            _loggerFactory = loggerFactory;
            _drakes = new Dictionary<string, Drake>();
        }

        /// <summary>
        /// Creates a Drake supervisor that monitors tasks from a specific wyvern output path
        /// </summary>
        /// <param name="taskFilePath">Path to the wyvern task markdown file</param>
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
            lock (_lock)
            {
                var name = drakeName ?? taskFilePath;

                if (_drakes.ContainsKey(name))
                {
                    throw new InvalidOperationException($"Drake with name '{name}' already exists");
                }

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

                // Create the Drake with specification path and project ID
                var drake = new Drake(
                    _koboldFactory,
                    taskTracker,
                    taskFilePath,
                    effectiveProvider,
                    config,
                    options,
                    specificationPath,
                    projectId,
                    logger
                );

                _drakes[name] = drake;

                return drake;
            }
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
        /// Loads tasks from a markdown file into the task tracker
        /// </summary>
        private void LoadTasksFromFile(TaskTracker taskTracker, string filePath)
        {
            // This is a placeholder - in a real implementation, you would parse the markdown file
            // For now, we'll just start with an empty tracker
            // TODO: Implement markdown parsing to restore task state
        }
    }
}
