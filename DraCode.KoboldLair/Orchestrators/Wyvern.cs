using System.Text.Json;
using DraCode.Agent;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Orchestrators
{
    /// <summary>
    /// Wyvern analyzes project specifications and creates organized, dependency-aware task lists.
    /// One Wyvern per project - reads Dragon specifications, categorizes work, and creates tasks for Drakes.
    /// </summary>
    public class Wyvern
    {
        private const string AnalysisJsonFileName = "analysis.json";

        private readonly string _projectName;
        private readonly string _specificationPath;
        private readonly WyvernAgent _analyzerAgent;
        private readonly string _provider;
        private readonly Dictionary<string, string> _config;
        private readonly AgentOptions _options;
        private readonly string _outputPath;
        private Specification? _specification;

        // Wyrm-specific provider settings (separate from Wyvern's own settings)
        private readonly string _wyrmProvider;
        private readonly Dictionary<string, string> _wyrmConfig;
        private readonly AgentOptions _wyrmOptions;

        // Git integration
        private readonly GitService? _gitService;

        private WyvernAnalysis? _analysis;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>
        /// Creates a new Wyvern for a project
        /// </summary>
        /// <param name="projectName">Name of the project</param>
        /// <param name="specificationPath">Path to the specification file</param>
        /// <param name="analyzerAgent">The Wyvern analyzer agent</param>
        /// <param name="provider">Provider for Wyvern analysis</param>
        /// <param name="config">Configuration for Wyvern</param>
        /// <param name="options">Agent options for Wyvern</param>
        /// <param name="outputPath">Output path for task files</param>
        /// <param name="wyrmProvider">Provider for Wyrm task delegation (optional, defaults to Wyvern's provider)</param>
        /// <param name="wyrmConfig">Configuration for Wyrm (optional, defaults to Wyvern's config)</param>
        /// <param name="wyrmOptions">Agent options for Wyrm (optional, defaults to Wyvern's options)</param>
        /// <param name="gitService">Git service for branch management (optional)</param>
        public Wyvern(
            string projectName,
            string specificationPath,
            WyvernAgent analyzerAgent,
            string provider,
            Dictionary<string, string> config,
            AgentOptions options,
            string outputPath,
            string? wyrmProvider = null,
            Dictionary<string, string>? wyrmConfig = null,
            AgentOptions? wyrmOptions = null,
            GitService? gitService = null)
        {
            _projectName = projectName;
            _specificationPath = specificationPath;
            _analyzerAgent = analyzerAgent;
            _provider = provider;
            _config = config;
            _options = options;
            _outputPath = outputPath;

            // Use Wyrm-specific settings if provided, otherwise fall back to Wyvern's settings
            _wyrmProvider = wyrmProvider ?? provider;
            _wyrmConfig = wyrmConfig ?? config;
            _wyrmOptions = wyrmOptions ?? options;

            // Git integration
            _gitService = gitService;

            // Try to load existing analysis from disk
            TryLoadAnalysis();
        }

        /// <summary>
        /// Gets the path to the analysis JSON file
        /// </summary>
        private string AnalysisJsonPath => Path.Combine(_outputPath, AnalysisJsonFileName);

        /// <summary>
        /// Tries to load analysis from disk if it exists
        /// </summary>
        private void TryLoadAnalysis()
        {
            if (_analysis != null)
                return;

            try
            {
                if (File.Exists(AnalysisJsonPath))
                {
                    var json = File.ReadAllText(AnalysisJsonPath);
                    _analysis = JsonSerializer.Deserialize<WyvernAnalysis>(json, _jsonOptions);
                }
            }
            catch
            {
                // Silently ignore load errors - will re-analyze if needed
                _analysis = null;
            }
        }

        /// <summary>
        /// Loads analysis from disk asynchronously
        /// </summary>
        /// <returns>The loaded analysis, or null if not found or failed to load</returns>
        public async Task<WyvernAnalysis?> LoadAnalysisAsync()
        {
            if (_analysis != null)
                return _analysis;

            try
            {
                if (File.Exists(AnalysisJsonPath))
                {
                    var json = await File.ReadAllTextAsync(AnalysisJsonPath);
                    _analysis = JsonSerializer.Deserialize<WyvernAnalysis>(json, _jsonOptions);
                    return _analysis;
                }
            }
            catch
            {
                // Silently ignore load errors
            }

            return null;
        }

        /// <summary>
        /// Saves the current analysis to disk
        /// </summary>
        public async Task SaveAnalysisAsync()
        {
            if (_analysis == null)
                return;

            try
            {
                // Ensure output directory exists
                Directory.CreateDirectory(_outputPath);

                var json = JsonSerializer.Serialize(_analysis, _jsonOptions);
                await File.WriteAllTextAsync(AnalysisJsonPath, json);
            }
            catch
            {
                // Silently ignore save errors - analysis is still in memory
            }
        }

        /// <summary>
        /// Loads specification and checks for new features
        /// </summary>
        public async Task<List<Feature>> GetNewFeaturesAsync(Specification specification)
        {
            _specification = specification;
            return specification.Features.Where(f => f.Status == FeatureStatus.New).ToList();
        }

        /// <summary>
        /// Marks features as assigned to Wyvern and creates git branches for each feature
        /// </summary>
        public void AssignFeatures(List<Feature> features)
        {
            foreach (var feature in features)
            {
                feature.Status = FeatureStatus.AssignedToWyvern;
                feature.UpdatedAt = DateTime.UtcNow;

                // Create git branch for the feature if git is available
                CreateFeatureBranchAsync(feature).GetAwaiter().GetResult();
            }
        }

        /// <summary>
        /// Creates a git branch for a feature
        /// </summary>
        private async Task CreateFeatureBranchAsync(Feature feature)
        {
            if (_gitService == null)
                return;

            try
            {
                if (!await _gitService.IsGitInstalledAsync())
                    return;

                if (!await _gitService.IsRepositoryAsync(_outputPath))
                    return;

                // Create branch name: feature/{id}-{sanitized-name}
                var branchName = _gitService.CreateFeatureBranchName(feature.Id, feature.Name);

                // Create the branch
                var created = await _gitService.CreateBranchAsync(_outputPath, branchName);
                if (created)
                {
                    feature.GitBranch = branchName;
                }
            }
            catch
            {
                // Silently ignore git errors - don't fail the workflow
            }
        }

        /// <summary>
        /// Gets the git branch name for a feature
        /// </summary>
        public string? GetFeatureBranch(string featureId)
        {
            return _specification?.Features.FirstOrDefault(f => f.Id == featureId)?.GitBranch;
        }

        /// <summary>
        /// Updates feature status based on task completion
        /// </summary>
        /// <param name="taskStatuses">Dictionary of task IDs to their status</param>
        public void UpdateFeatureStatus(Dictionary<string, TaskStatus> taskStatuses)
        {
            if (_specification == null || _analysis == null)
                return;

            foreach (var feature in _specification.Features.Where(f => f.Status != FeatureStatus.Completed))
            {
                var featureTasks = GetTasksForFeature(feature.Id);

                if (!featureTasks.Any())
                    continue;

                // Check if any task is being worked on
                var hasWorkingTasks = featureTasks.Any(taskId =>
                    taskStatuses.TryGetValue(taskId, out var status) &&
                    (status == TaskStatus.Working || status == TaskStatus.NotInitialized));

                // Check if all tasks are done
                var allTasksDone = featureTasks.All(taskId =>
                    taskStatuses.TryGetValue(taskId, out var status) &&
                    status == TaskStatus.Done);

                // Update feature status
                if (allTasksDone && feature.Status != FeatureStatus.Completed)
                {
                    feature.Status = FeatureStatus.Completed;
                    feature.UpdatedAt = DateTime.UtcNow;
                }
                else if (hasWorkingTasks && feature.Status == FeatureStatus.AssignedToWyvern)
                {
                    feature.Status = FeatureStatus.InProgress;
                    feature.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        /// <summary>
        /// Gets all task IDs associated with a feature
        /// </summary>
        private List<string> GetTasksForFeature(string featureId)
        {
            if (_analysis == null)
                return new List<string>();

            return _analysis.Areas
                .SelectMany(area => area.Tasks)
                .Where(task => task.FeatureId == featureId)
                .Select(task => task.Id)
                .ToList();
        }

        /// <summary>
        /// Links tasks to features based on feature context in task description
        /// Call this after analysis to associate tasks with features
        /// </summary>
        public void LinkTasksToFeatures()
        {
            if (_specification == null || _analysis == null)
                return;

            foreach (var feature in _specification.Features.Where(f => f.Status == FeatureStatus.AssignedToWyvern))
            {
                // Find tasks that mention this feature
                var relatedTasks = _analysis.Areas
                    .SelectMany(area => area.Tasks)
                    .Where(task =>
                        task.Description.Contains(feature.Name, StringComparison.OrdinalIgnoreCase) ||
                        task.Name.Contains(feature.Name, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Link tasks to feature
                foreach (var task in relatedTasks)
                {
                    task.FeatureId = feature.Id;

                    // Add task ID to feature's task list
                    if (!feature.TaskIds.Contains(task.Id))
                    {
                        feature.TaskIds.Add(task.Id);
                    }
                }
            }
        }

        /// <summary>
        /// Gets feature completion report
        /// </summary>
        public string GetFeatureStatusReport()
        {
            if (_specification == null)
                return "No specification loaded.";

            var report = new System.Text.StringBuilder();
            report.AppendLine("# Feature Status Report");
            report.AppendLine();

            var grouped = _specification.Features.GroupBy(f => f.Status);

            foreach (var group in grouped.OrderBy(g => g.Key))
            {
                report.AppendLine($"## {group.Key} ({group.Count()})");
                report.AppendLine();

                foreach (var feature in group)
                {
                    var taskCount = feature.TaskIds.Count;
                    var icon = group.Key switch
                    {
                        FeatureStatus.New => "🆕",
                        FeatureStatus.AssignedToWyvern => "📋",
                        FeatureStatus.InProgress => "🔨",
                        FeatureStatus.Completed => "✅",
                        _ => "❓"
                    };

                    report.AppendLine($"{icon} **{feature.Name}** (Priority: {feature.Priority})");
                    report.AppendLine($"   {feature.Description}");
                    report.AppendLine($"   Tasks: {taskCount}");
                    report.AppendLine();
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Analyzes the specification and creates organized task structure
        /// Includes new features in the analysis context
        /// </summary>
        public async Task<WyvernAnalysis> AnalyzeProjectAsync(Specification? specification = null)
        {
            if (specification != null)
            {
                _specification = specification;
            }

            if (!File.Exists(_specificationPath))
            {
                throw new FileNotFoundException($"Specification not found: {_specificationPath}");
            }

            var specContent = await File.ReadAllTextAsync(_specificationPath);

            // Get new features to include in analysis
            var newFeatures = _specification?.Features.Where(f => f.Status == FeatureStatus.New).ToList() ?? new List<Feature>();

            // Build enhanced prompt with features
            var prompt = specContent;
            if (newFeatures.Any())
            {
                prompt += "\n\n## New Features to Implement:\n\n";
                foreach (var feature in newFeatures)
                {
                    prompt += $"### {feature.Name} (Priority: {feature.Priority})\n";
                    prompt += $"{feature.Description}\n\n";
                }

                // Mark features as assigned
                AssignFeatures(newFeatures);
            }

            var analysisJson = await _analyzerAgent.AnalyzeSpecificationAsync(prompt);

            try
            {
                _analysis = JsonSerializer.Deserialize<WyvernAnalysis>(analysisJson, _jsonOptions);

                if (_analysis == null)
                {
                    throw new InvalidOperationException("Failed to parse Wyvern analysis");
                }

                _analysis.AnalyzedAt = DateTime.UtcNow;
                _analysis.SpecificationPath = _specificationPath;

                // Link features to analysis
                if (_specification != null)
                {
                    _analysis.ProcessedFeatures = _specification.Features
                        .Where(f => f.Status == FeatureStatus.AssignedToWyvern)
                        .Select(f => f.Id)
                        .ToList();
                }

                // Persist analysis to disk for recovery after restart
                await SaveAnalysisAsync();

                return _analysis;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse Wyvern analysis JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates task files for each area containing individual tasks from the analysis.
        /// Can optionally process only specific areas (for reprocessing pending areas).
        /// </summary>
        /// <param name="areasToProcess">Optional list of area names to process. If null, processes all areas.</param>
        /// <param name="existingTaskFiles">Optional existing task files dictionary to merge with.</param>
        /// <returns>Dictionary of area names to task file paths, and list of areas that failed processing.</returns>
        public Task<(Dictionary<string, string> TaskFiles, List<string> FailedAreas)> CreateTasksAsync(
            List<string>? areasToProcess = null,
            Dictionary<string, string>? existingTaskFiles = null)
        {
            if (_analysis == null)
            {
                throw new InvalidOperationException("Must call AnalyzeProjectAsync() first");
            }

            var taskFiles = existingTaskFiles != null
                ? new Dictionary<string, string>(existingTaskFiles)
                : new Dictionary<string, string>();
            var failedAreas = new List<string>();

            // Determine which areas to process
            var areas = areasToProcess != null
                ? _analysis.Areas.Where(a => areasToProcess.Contains(a.Name, StringComparer.OrdinalIgnoreCase)).ToList()
                : _analysis.Areas;

            foreach (var area in areas)
            {
                try
                {
                    // Simplified naming: {area}-tasks.md (project folder provides context)
                    var areaOutputPath = Path.Combine(_outputPath, $"{area.Name.ToLower()}-tasks.md");

                    // Load existing tracker if file exists to preserve task statuses
                    var tracker = new TaskTracker();
                    var existingTaskIds = new HashSet<string>();

                    if (File.Exists(areaOutputPath))
                    {
                        tracker.LoadFromFile(areaOutputPath);
                        // Index existing tasks by their task ID (e.g., "BE-001")
                        foreach (var existingTask in tracker.GetAllTasks())
                        {
                            // Extract task ID from task format: [frontend-1] Task name...
                            var match = System.Text.RegularExpressions.Regex.Match(
                                existingTask.Task, @"^\[([a-zA-Z]+-\d+)\]");
                            if (match.Success)
                            {
                                existingTaskIds.Add(match.Groups[1].Value);
                            }
                        }
                    }

                    foreach (var task in area.Tasks.OrderBy(t => t.DependencyLevel))
                    {
                        // Skip tasks that already exist - preserve their current status
                        if (existingTaskIds.Contains(task.Id))
                        {
                            continue;
                        }

                        // Format: [task-id] Task name: Description
                        var deps = task.Dependencies.Any()
                            ? $" (depends on: {string.Join(", ", task.Dependencies)})"
                            : "";
                        var taskDescription = $"[{task.Id}] {task.Name}{deps}";

                        var taskRecord = tracker.AddTask(taskDescription);

                        // Set the recommended agent type if available
                        if (!string.IsNullOrEmpty(task.AgentType))
                        {
                            tracker.UpdateTask(taskRecord, TaskStatus.Unassigned, task.AgentType);
                        }
                    }

                    // Save the tracker with all individual tasks
                    tracker.SaveToFile(areaOutputPath, $"KoboldLair {area.Name} Tasks");

                    taskFiles[area.Name] = areaOutputPath;
                }
                catch (Exception)
                {
                    // Track failed areas for reprocessing
                    failedAreas.Add(area.Name);
                }
            }

            return Task.FromResult((taskFiles, failedAreas));
        }

        /// <summary>
        /// Gets the list of all area names from the analysis
        /// </summary>
        public List<string> GetAllAreaNames()
        {
            return _analysis?.Areas.Select(a => a.Name).ToList() ?? new List<string>();
        }

        private string CreateOrchestratorInput(WorkArea area)
        {
            var taskDescriptions = area.Tasks
                .OrderBy(t => t.DependencyLevel)
                .Select(t =>
                {
                    var deps = t.Dependencies.Any() ? $" (depends on: {string.Join(", ", t.Dependencies)})" : "";
                    return $"- [{t.Id}] {t.Name}{deps}: {t.Description}";
                });

            return $"I need help organizing work for the {area.Name} area.\n\nTasks:\n{string.Join("\n", taskDescriptions)}";
        }

        public string GenerateReport()
        {
            if (_analysis == null) return "No analysis available.";

            var report = new System.Text.StringBuilder();
            report.AppendLine($"# Wyvern Analysis: {_analysis.ProjectName}");
            report.AppendLine($"Total Tasks: {_analysis.TotalTasks}");

            foreach (var area in _analysis.Areas)
            {
                report.AppendLine($"\n## {area.Name}");
                foreach (var task in area.Tasks.OrderBy(t => t.DependencyLevel))
                {
                    report.AppendLine($"- [{task.Id}] {task.Name} (Level {task.DependencyLevel})");
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Gets the current analysis. Attempts to load from disk if not in memory.
        /// </summary>
        public WyvernAnalysis? Analysis
        {
            get
            {
                if (_analysis == null)
                {
                    TryLoadAnalysis();
                }
                return _analysis;
            }
        }

        /// <summary>
        /// Gets the path to the persisted analysis JSON file
        /// </summary>
        public string AnalysisPath => AnalysisJsonPath;

        public string ProjectName => _projectName;
        public string SpecificationPath => _specificationPath;
    }
}
