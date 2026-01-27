using System.Text.Json;
using DraCode.Agent;
using DraCode.KoboldLair.Server.Agents;
using DraCode.KoboldLair.Server.Wyvern;

namespace DraCode.KoboldLair.Server.Projects
{
    /// <summary>
    /// Wyrm analyzes project specifications and creates organized, dependency-aware task lists.
    /// One Wyrm per project - reads Dragon specifications, categorizes work, and creates tasks for Drakes.
    /// </summary>
    public class Wyrm
    {
        private readonly string _projectName;
        private readonly string _specificationPath;
        private readonly WyrmAnalyzerAgent _analyzerAgent;
        private readonly string _provider;
        private readonly Dictionary<string, string> _config;
        private readonly AgentOptions _options;
        private readonly string _outputPath;

        private WyrmAnalysis? _analysis;

        /// <summary>
        /// Creates a new Wyrm for a project
        /// </summary>
        public Wyrm(
            string projectName,
            string specificationPath,
            WyrmAnalyzerAgent analyzerAgent,
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
        /// Analyzes the specification and creates organized task structure
        /// </summary>
        public async Task<WyrmAnalysis> AnalyzeProjectAsync()
        {
            if (!File.Exists(_specificationPath))
            {
                throw new FileNotFoundException($"Specification not found: {_specificationPath}");
            }

            var specContent = await File.ReadAllTextAsync(_specificationPath);
            var analysisJson = await _analyzerAgent.AnalyzeSpecificationAsync(specContent);

            try
            {
                _analysis = JsonSerializer.Deserialize<WyrmAnalysis>(analysisJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (_analysis == null)
                {
                    throw new InvalidOperationException("Failed to parse Wyrm analysis");
                }

                _analysis.AnalyzedAt = DateTime.UtcNow;
                _analysis.SpecificationPath = _specificationPath;

                return _analysis;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse Wyrm analysis JSON: {ex.Message}");
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
                
                // Use WyvernRunner static method
                await WyvernRunner.RunAsync(
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
            report.AppendLine($"# Wyrm Analysis: {_analysis.ProjectName}");
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

        public WyrmAnalysis? Analysis => _analysis;
        public string ProjectName => _projectName;
        public string SpecificationPath => _specificationPath;
    }

    public class WyrmAnalysis
    {
        public string ProjectName { get; set; } = "";
        public List<WorkArea> Areas { get; set; } = new();
        public int TotalTasks { get; set; }
        public string EstimatedComplexity { get; set; } = "medium";
        public DateTime AnalyzedAt { get; set; }
        public string SpecificationPath { get; set; } = "";
    }

    public class WorkArea
    {
        public string Name { get; set; } = "";
        public List<WyrmTask> Tasks { get; set; } = new();
    }

    public class WyrmTask
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string AgentType { get; set; } = "coding";
        public string Complexity { get; set; } = "medium";
        public List<string> Dependencies { get; set; } = new();
        public int DependencyLevel { get; set; }
        public string Priority { get; set; } = "medium";
    }
}