using System.Text.Json;
using DraCode.Agent;
using DraCode.KoboldLair.Server.Agents.Wyrm;
using DraCode.KoboldLair.Server.Models;

namespace DraCode.KoboldLair.Server.Agents.Wyvern
{
    /// <summary>
    /// Wyvern analyzes project specifications and creates organized, dependency-aware task lists.
    /// One Wyvern per project - reads Dragon specifications, categorizes work, and creates tasks for Drakes.
    /// </summary>
    public class Wyvern
    {
        private readonly string _projectName;
        private readonly string _specificationPath;
        private readonly WyvernAnalyzerAgent _analyzerAgent;
        private readonly string _provider;
        private readonly Dictionary<string, string> _config;
        private readonly AgentOptions _options;
        private readonly string _outputPath;
        private Specification? _specification;

        private WyvernAnalysis? _analysis;

        /// <summary>
        /// Creates a new Wyvern for a project
        /// </summary>
        public Wyvern(
            string projectName,
            string specificationPath,
            WyvernAnalyzerAgent analyzerAgent,
            string provider,
            Dictionary<string, string> config,
            AgentOptions options,
            string outputPath)
        {
            _projectName = projectName;
            _specificationPath = specificationPath;
            _analyzerAgent = analyzerAgent;
            _provider = provider;
            _config = config;
            _options = options;
            _outputPath = outputPath;
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
        /// Marks features as assigned to Wyvern
        /// </summary>
        public void AssignFeatures(List<Feature> features)
        {
            foreach (var feature in features)
            {
                feature.Status = FeatureStatus.AssignedToWyvern;
                feature.UpdatedAt = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Updates feature status based on task completion
        /// </summary>
        /// <param name="taskStatuses">Dictionary of task IDs to their status</param>
        public void UpdateFeatureStatus(Dictionary<string, Wyrm.TaskStatus> taskStatuses)
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
                    (status == Wyrm.TaskStatus.Working || status == Wyrm.TaskStatus.NotInitialized));

                // Check if all tasks are done
                var allTasksDone = featureTasks.All(taskId =>
                    taskStatuses.TryGetValue(taskId, out var status) && 
                    status == Wyrm.TaskStatus.Done);

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
                _analysis = JsonSerializer.Deserialize<WyvernAnalysis>(analysisJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

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

                return _analysis;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse Wyvern analysis JSON: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates tasks using the orchestrator for each area
        /// </summary>
        public async Task<Dictionary<string, string>> CreateTasksAsync()
        {
            if (_analysis == null)
            {
                throw new InvalidOperationException("Must call AnalyzeProjectAsync() first");
            }

            var taskFiles = new Dictionary<string, string>();

            foreach (var area in _analysis.Areas)
            {
                var areaOutputPath = Path.Combine(_outputPath, $"{_projectName}-{area.Name.ToLower()}-tasks.md");
                var orchestratorInput = CreateOrchestratorInput(area);
                
                // Use WyrmRunner static method
                await WyrmRunner.RunAsync(
                    _provider,
                    orchestratorInput,
                    _options,
                    _config,
                    outputMarkdownPath: areaOutputPath
                );
                
                taskFiles[area.Name] = areaOutputPath;
            }

            return taskFiles;
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

        public WyvernAnalysis? Analysis => _analysis;
        public string ProjectName => _projectName;
        public string SpecificationPath => _specificationPath;
    }

    public class WyvernAnalysis
    {
        public string ProjectName { get; set; } = "";
        public List<WorkArea> Areas { get; set; } = new();
        public int TotalTasks { get; set; }
        public string EstimatedComplexity { get; set; } = "medium";
        public DateTime AnalyzedAt { get; set; }
        public string SpecificationPath { get; set; } = "";
        public List<string> ProcessedFeatures { get; set; } = new();
    }

    public class WorkArea
    {
        public string Name { get; set; } = "";
        public List<WyvernTask> Tasks { get; set; } = new();
    }

    public class WyvernTask
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string AgentType { get; set; } = "coding";
        public string Complexity { get; set; } = "medium";
        public List<string> Dependencies { get; set; } = new();
        public int DependencyLevel { get; set; }
        public string Priority { get; set; } = "medium";
        public string? FeatureId { get; set; }
    }
}