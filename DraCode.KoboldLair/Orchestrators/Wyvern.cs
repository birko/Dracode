using System.Text.Json;
using Birko.AI;
using Birko.AI.Agents;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;
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

        /// <summary>Database the created tasks are written to (TASK-091); set by WyvernFactory. Null = files only.</summary>
        public ITaskRepository? TaskRepository { get; set; }

        /// <summary>Project id the task rows carry; set by WyvernFactory together with <see cref="TaskRepository"/>.</summary>
        public string? ProjectId { get; set; }
        private readonly string _specificationPath;
        private readonly WyvernAgent _analyzerAgent;
        private readonly string _provider;
        private readonly Dictionary<string, string> _config;
        private readonly AgentOptions _options;
        private readonly string _outputPath;
        private Specification? _specification;
        private readonly object _analysisLock = new object();
        private readonly ILogger<Wyvern>? _logger;

        // Wyrm-specific provider settings (separate from Wyvern's own settings)
        private readonly string _wyrmProvider;
        private readonly Dictionary<string, string> _wyrmConfig;
        private readonly AgentOptions _wyrmOptions;

        // Git integration
        private readonly GitService? _gitService;

        // For existing projects: scan the actual source directory instead of workspace
        private readonly string? _workspaceScanPath;

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
        /// <param name="logger">Optional logger for diagnostics</param>
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
            GitService? gitService = null,
            ILogger<Wyvern>? logger = null,
            string? workspaceScanPath = null)
        {
            _projectName = projectName;
            _specificationPath = specificationPath;
            _analyzerAgent = analyzerAgent;
            _provider = provider;
            _config = config;
            _options = options;
            _outputPath = outputPath;
            _logger = logger;
            _workspaceScanPath = workspaceScanPath;

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
                    var json = File.ReadAllTextAsync(AnalysisJsonPath).GetAwaiter().GetResult();
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
            WyvernAnalysis? analysisToSave;
            
            lock (_analysisLock)
            {
                if (_analysis == null)
                    return;
                    
                // Take snapshot to avoid holding lock during I/O
                analysisToSave = _analysis;
            }

            try
            {
                // Ensure output directory exists
                Directory.CreateDirectory(_outputPath);

                var json = JsonSerializer.Serialize(analysisToSave, _jsonOptions);
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
            return specification.Features.Where(f => f.Status == FeatureStatus.Ready || f.Status == FeatureStatus.Draft).ToList();
        }

        /// <summary>
        /// Marks features as assigned to Wyvern and creates git branches for each feature (async version)
        /// </summary>
        public async Task AssignFeaturesAsync(List<Feature> features)
        {
            foreach (var feature in features)
            {
                feature.Status = FeatureStatus.AssignedToWyvern;
                feature.UpdatedAt = DateTime.UtcNow;

                // Create git branch for the feature if git is available
                await CreateFeatureBranchAsync(feature);
            }
        }

        /// <summary>
        /// Marks features as assigned to Wyvern and creates git branches for each feature
        /// Note: Prefer AssignFeaturesAsync() for non-blocking operation.
        /// </summary>
        public void AssignFeatures(List<Feature> features)
        {
            AssignFeaturesAsync(features).ConfigureAwait(false).GetAwaiter().GetResult();
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
        /// Gets the feature name by its ID
        /// </summary>
        public string? GetFeatureNameById(string featureId)
        {
            return _specification?.Features.FirstOrDefault(f => f.Id == featureId)?.Name;
        }

        /// <summary>
        /// Updates feature status based on task completion.
        /// Returns list of features that just transitioned to Completed (name, gitBranch).
        /// </summary>
        /// <param name="taskStatuses">Dictionary of task IDs to their status</param>
        /// <returns>List of newly completed features with their branch names</returns>
        public List<(string Name, string? GitBranch)> UpdateFeatureStatus(Dictionary<string, TaskStatus> taskStatuses)
        {
            var newlyCompleted = new List<(string Name, string? GitBranch)>();

            if (_specification == null || _analysis == null)
                return newlyCompleted;

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
                    newlyCompleted.Add((feature.Name, feature.GitBranch));
                }
                else if (hasWorkingTasks && feature.Status == FeatureStatus.AssignedToWyvern)
                {
                    feature.Status = FeatureStatus.InProgress;
                    feature.UpdatedAt = DateTime.UtcNow;
                }
            }

            return newlyCompleted;
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

        private readonly HashSet<string> _areasWithoutNewTasks = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Areas of the last <see cref="CreateTasksAsync"/> whose tasks all existed already, so no task file was written
        /// </summary>
        public IReadOnlyCollection<string> AreasWithoutNewTasks => _areasWithoutNewTasks;

        private static readonly System.Text.RegularExpressions.Regex TaskLine =
            new(@"^\[(?<id>[^\]]+)\]\s*(?<name>.*?)(?:\s*\(depends on:[^)]*\))?\s*$");

        /// <summary>
        /// The tasks already written to a project's task files (<c>tasks/*-tasks.md</c>): Wyvern id, name and status
        /// </summary>
        public static List<(string Id, string Name, TaskStatus Status)> LoadExistingTaskSummaries(string outputPath)
        {
            var summaries = new List<(string Id, string Name, TaskStatus Status)>();
            var taskDir = Path.Combine(outputPath, "tasks");
            if (!Directory.Exists(taskDir))
                return summaries;

            foreach (var file in Directory.GetFiles(taskDir, "*-tasks.md"))
            {
                var tracker = new TaskTracker();
                tracker.LoadFromFile(file);
                foreach (var record in tracker.GetAllTasks())
                {
                    var match = TaskLine.Match(record.Task);
                    if (match.Success)
                        summaries.Add((match.Groups["id"].Value, match.Groups["name"].Value, record.Status));
                }
            }
            return summaries;
        }

        private static string NormalizeTaskName(string name) =>
            System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", " ").Trim();

        /// <summary>
        /// Links each analysed task to the feature its <c>featureId</c> names. A reply that echoes the feature's name
        /// instead of its id is accepted on an exact (case-insensitive) match; anything else unlinks the task. Each
        /// linked task id is added to its feature's <see cref="Feature.TaskIds"/>.
        /// </summary>
        public static void LinkTasksToFeatures(WyvernAnalysis analysis, IReadOnlyCollection<Feature> features)
        {
            foreach (var task in analysis.Areas.SelectMany(area => area.Tasks))
            {
                if (string.IsNullOrWhiteSpace(task.FeatureId))
                {
                    task.FeatureId = null;
                    continue;
                }

                var feature = features.FirstOrDefault(f => string.Equals(f.Id, task.FeatureId, StringComparison.Ordinal))
                    ?? features.FirstOrDefault(f => string.Equals(f.Name, task.FeatureId.Trim(), StringComparison.OrdinalIgnoreCase));

                task.FeatureId = feature?.Id;
                if (feature != null && !feature.TaskIds.Contains(task.Id))
                    feature.TaskIds.Add(task.Id);
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
                    var displayStatus = group.Key;

                    var icon = displayStatus switch
                    {
                        FeatureStatus.Draft => "📝",
                        FeatureStatus.Ready => "✅",
                        FeatureStatus.AssignedToWyvern => "📋",
                        FeatureStatus.InProgress => "🔨",
                        FeatureStatus.Completed => "🎉",
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
        /// Scans the workspace directory to build a project structure map.
        /// Excludes common build/cache directories.
        /// </summary>
        private ProjectStructure ScanWorkspaceStructure()
        {
            var structure = new ProjectStructure();
            var excludedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".git", "node_modules", "bin", "obj", ".vs", ".vscode",
                "dist", "build", "target", "__pycache__", ".next", ".nuxt"
            };

            // Use workspace scan path if set (existing projects), otherwise default to output path
            var scanPath = _workspaceScanPath ?? _outputPath;

            try
            {
                if (Directory.Exists(scanPath))
                {
                    var files = Directory.GetFiles(scanPath, "*.*", SearchOption.AllDirectories)
                        .Select(f => Path.GetRelativePath(scanPath, f))
                        .Where(f => !excludedDirs.Any(d => f.Split(Path.DirectorySeparatorChar).Contains(d)))
                        .OrderBy(f => f)
                        .ToList();

                    structure.ExistingFiles = files;
                }
            }
            catch
            {
                // If scanning fails, continue with empty structure
            }

            return structure;
        }

        /// <summary>
        /// Analyzes project structure using LLM to extract conventions and guidelines.
        /// If no existing files, uses the structure proposed by Wyvern analysis.
        /// </summary>
        private async Task<ProjectStructure> AnalyzeProjectStructureAsync(ProjectStructure scannedStructure, string specificationContent, ProjectStructure? proposedStructure = null)
        {
            // If we have a proposed structure from Wyvern analysis, use it as the base
            if (proposedStructure != null)
            {
                // For new projects with no files, use the proposed structure directly
                if (!scannedStructure.ExistingFiles.Any())
                {
                    return proposedStructure;
                }

                // For existing projects, merge proposed with scanned
                // Scanned files take precedence, but we keep proposed guidelines
                proposedStructure.ExistingFiles = scannedStructure.ExistingFiles;
                return proposedStructure;
            }

            // Fallback: No proposed structure, scan existing files if available
            if (!scannedStructure.ExistingFiles.Any())
            {
                // No files yet and no proposed structure - return basic structure
                return scannedStructure;
            }

            var structurePrompt = $@"Analyze this project's file structure and provide organization guidelines.

EXISTING FILES:
{string.Join("\n", scannedStructure.ExistingFiles.Take(100))}

SPECIFICATION:
{specificationContent}

Respond with ONLY valid JSON (no markdown, no explanations):
{{
  ""namingConventions"": {{
    ""csharp-classes"": ""PascalCase"",
    ""js-modules"": ""camelCase"",
    ""config-files"": ""kebab-case""
  }},
  ""directoryPurposes"": {{
    ""src/"": ""Main source code"",
    ""tests/"": ""Unit and integration tests""
  }},
  ""fileLocationGuidelines"": {{
    ""controller"": ""src/controllers/"",
    ""model"": ""src/models/""
  }},
  ""architectureNotes"": ""Brief notes about project architecture and organization""
}}";

            try
            {
                var jsonContent = await _analyzerAgent.AnalyzeSpecificationAsync(structurePrompt);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var analyzedStructure = JsonSerializer.Deserialize<ProjectStructure>(jsonContent, options);
                if (analyzedStructure != null)
                {
                    // Merge with scanned files
                    analyzedStructure.ExistingFiles = scannedStructure.ExistingFiles;
                    return analyzedStructure;
                }
            }
            catch
            {
                // If LLM analysis fails, return scanned structure
            }

            return scannedStructure;
        }

        /// <summary>
        /// Analyzes the specification and creates organized task structure.
        /// Includes new features and optional Wyrm recommendations in the analysis context.
        /// </summary>
        /// <param name="specification">Optional specification to analyze</param>
        /// <param name="wyrmRecommendation">Optional Wyrm pre-analysis recommendations to guide Wyvern</param>
        public async Task<WyvernAnalysis> AnalyzeProjectAsync(Specification? specification = null, WyrmRecommendation? wyrmRecommendation = null)
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

            var newFeatures = _specification?.Features.Where(f => f.Status == FeatureStatus.Ready || f.Status == FeatureStatus.Draft).ToList() ?? new List<Feature>();

            // Build enhanced prompt with features and Wyrm recommendations
            var prompt = specContent;
            
            if (newFeatures.Any())
            {
                prompt += "\n\n## New Features to Implement:\n\n";
                foreach (var feature in newFeatures)
                {
                    prompt += $"### {feature.Name} (id: {feature.Id}, Priority: {feature.Priority})\n";
                    prompt += $"{feature.Description}\n\n";
                }
            }

            // On re-analysis, show the work already planned so the model adds only what the new features need
            var existingTasks = LoadExistingTaskSummaries(_outputPath);
            if (existingTasks.Any())
            {
                prompt += "\n\n## Existing Tasks (already planned — do NOT re-emit; create tasks only for New Features):\n\n";
                foreach (var existing in existingTasks)
                {
                    prompt += $"- [{existing.Id}] {existing.Name} — {existing.Status}\n";
                }
            }

            // Add Wyrm recommendations as hints
            if (wyrmRecommendation != null)
            {
                prompt += "\n\n## Wyrm Pre-Analysis Recommendations:\n\n";
                prompt += $"**Analysis Summary:** {wyrmRecommendation.AnalysisSummary}\n\n";
                
                if (wyrmRecommendation.RecommendedLanguages.Any())
                {
                    prompt += $"**Recommended Languages:** {string.Join(", ", wyrmRecommendation.RecommendedLanguages)}\n\n";
                }
                
                if (wyrmRecommendation.TechnicalStack.Any())
                {
                    prompt += $"**Technical Stack:** {string.Join(", ", wyrmRecommendation.TechnicalStack)}\n\n";
                }
                
                if (wyrmRecommendation.RecommendedAgentTypes.Any())
                {
                    prompt += "**Recommended Agent Types:**\n";
                    foreach (var kvp in wyrmRecommendation.RecommendedAgentTypes)
                    {
                        prompt += $"- {kvp.Key}: `{kvp.Value}`\n";
                    }
                    prompt += "\n";
                }
                
                if (wyrmRecommendation.SuggestedAreas.Any())
                {
                    prompt += $"**Suggested Task Areas:** {string.Join(", ", wyrmRecommendation.SuggestedAreas)}\n\n";
                }
                
                prompt += $"**Estimated Complexity:** {wyrmRecommendation.Complexity}\n\n";
                
                if (!string.IsNullOrEmpty(wyrmRecommendation.Notes))
                {
                    prompt += $"**Additional Notes:** {wyrmRecommendation.Notes}\n\n";
                }

                if (wyrmRecommendation.Constraints.Any())
                {
                    prompt += "**⛔ Constraints (from pre-analysis — MUST be preserved in your analysis):**\n";
                    foreach (var constraint in wyrmRecommendation.Constraints)
                    {
                        prompt += $"- {constraint}\n";
                    }
                    prompt += "\n";
                }

                if (wyrmRecommendation.OutOfScope.Any())
                {
                    prompt += "**Out of Scope (do NOT create tasks for these):**\n";
                    foreach (var item in wyrmRecommendation.OutOfScope)
                    {
                        prompt += $"- {item}\n";
                    }
                    prompt += "\n";
                }

                prompt += "Use these recommendations as guidance for your analysis, but feel free to adjust based on the full specification.\n\n";
            }

            string analysisJson;
            try
            {
                analysisJson = await _analyzerAgent.AnalyzeSpecificationAsync(prompt);
            }
            catch (InvalidOperationException ex)
            {
                LogUnusableReply(ex.Message);
                throw;
            }

            try
            {
                _analysis = JsonSerializer.Deserialize<WyvernAnalysis>(analysisJson, _jsonOptions);

                if (_analysis == null)
                {
                    throw new InvalidOperationException("Failed to parse Wyvern analysis");
                }

                // A reply that parses but carries no tasks is not "nothing to do" — it is a reply we could not use
                if (_analysis.Areas.All(a => a.Tasks.Count == 0))
                {
                    LogUnusableReply("analysis has no tasks");
                    throw new InvalidOperationException(
                        "Wyvern analysis contained no tasks — the model reply was empty or not in the expected shape");
                }

                // Link tasks to features, then assign only the features that received a task: one the model left
                // without work stays Ready, so the next analysis offers it again. Persist once — the sidecar is what
                // the next analysis, Drake's branch choice and Sage read.
                if (_specification != null)
                {
                    LinkTasksToFeatures(_analysis, _specification.Features);

                    var linkedFeatureIds = _analysis.Areas.SelectMany(a => a.Tasks)
                        .Where(t => t.FeatureId != null).Select(t => t.FeatureId!).ToHashSet();
                    var covered = newFeatures.Where(f => linkedFeatureIds.Contains(f.Id)).ToList();
                    foreach (var missed in newFeatures.Except(covered))
                    {
                        _logger?.LogWarning("Wyvern produced no task for feature {Feature} ({FeatureId}); it stays Ready for the next analysis",
                            missed.Name, missed.Id);
                    }

                    await AssignFeaturesAsync(covered);
                    if (covered.Any() || linkedFeatureIds.Any())
                        await SpecificationService.PersistFeaturesAsync(_specification);
                }

                _analysis.AnalyzedAt = DateTime.UtcNow;
                _analysis.SpecificationPath = _specificationPath;

                // Extract proposed structure from analysis if available
                var proposedStructure = _analysis.Structure;

                // Scan and analyze project structure
                var scannedStructure = ScanWorkspaceStructure();
                _analysis.Structure = await AnalyzeProjectStructureAsync(scannedStructure, specContent, proposedStructure);

                // Link features to analysis
                if (_specification != null)
                {
                    _analysis.ProcessedFeatures = _specification.Features
                        .Where(f => f.Status == FeatureStatus.AssignedToWyvern)
                        .Select(f => f.Id)
                        .ToList();
                }

                // Gap 9 fix: Programmatic constraint merge — if Wyvern's LLM failed to preserve
                // Wyrm constraints in its analysis, copy them over directly to guarantee propagation.
                if (wyrmRecommendation != null)
                {
                    if (wyrmRecommendation.Constraints.Any())
                    {
                        foreach (var constraint in wyrmRecommendation.Constraints)
                        {
                            if (!_analysis.Constraints.Contains(constraint, StringComparer.OrdinalIgnoreCase))
                            {
                                _analysis.Constraints.Add(constraint);
                            }
                        }
                    }

                    if (wyrmRecommendation.OutOfScope.Any())
                    {
                        foreach (var item in wyrmRecommendation.OutOfScope)
                        {
                            if (!_analysis.OutOfScope.Contains(item, StringComparer.OrdinalIgnoreCase))
                            {
                                _analysis.OutOfScope.Add(item);
                            }
                        }
                    }
                }

                // Validate and fix task dependencies
                ValidateAndFixTaskDependencies(_analysis);

                // Persist analysis to disk for recovery after restart
                await SaveAnalysisAsync();

                return _analysis;
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException($"Failed to parse Wyvern analysis JSON: {ex.Message}");
            }
        }

        private void LogUnusableReply(string reason)
        {
            var raw = _analyzerAgent.LastRawResponse ?? string.Empty;
            _logger?.LogWarning(
                "Wyvern reply unusable for {SpecificationPath} ({Reason}). Raw reply, {Length} chars: {Excerpt}",
                _specificationPath, reason, raw.Length, raw.Length > 2000 ? raw[..2000] + "…" : raw);
        }

        /// <summary>
        /// Creates task files for each area containing individual tasks from the analysis.
        /// Can optionally process only specific areas (for reprocessing pending areas).
        /// </summary>
        /// <param name="areasToProcess">Optional list of area names to process. If null, processes all areas.</param>
        /// <param name="existingTaskFiles">Optional existing task files dictionary to merge with.</param>
        /// <returns>Dictionary of area names to task file paths, and list of areas that failed processing.</returns>
        public async Task<(Dictionary<string, string> TaskFiles, List<string> FailedAreas)> CreateTasksAsync(
            List<string>? areasToProcess = null,
            Dictionary<string, string>? existingTaskFiles = null)
        {
            if (_analysis == null)
            {
                throw new InvalidOperationException("Must call AnalyzeProjectAsync() first");
            }

            // Area names are matched case-insensitively ("CLI" from an earlier run and "cli" now are one file)
            var taskFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (existingArea, existingPath) in existingTaskFiles ?? new Dictionary<string, string>())
                taskFiles[existingArea] = existingPath;
            var failedAreas = new List<string>();

            // Determine which areas to process
            var areas = areasToProcess != null
                ? _analysis.Areas.Where(a => areasToProcess.Contains(a.Name, StringComparer.OrdinalIgnoreCase)).ToList()
                : _analysis.Areas;

            _areasWithoutNewTasks.Clear();

            // A re-analysis re-plans against everything already written, in every area: skip a task whose id exists
            // anywhere, or whose name matches a task that is already Done (a repeated not-yet-done task is kept)
            var existingTasks = LoadExistingTaskSummaries(_outputPath);
            var existingTaskIds = existingTasks.Select(t => t.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var doneTaskNames = existingTasks.Where(t => t.Status == TaskStatus.Done)
                .Select(t => NormalizeTaskName(t.Name)).ToHashSet();

            foreach (var area in areas)
            {
                try
                {
                    // Simplified naming: {area}-tasks.md (project folder provides context)
                    // Sanitize area name: replace spaces and special characters (/, \, :, etc.) with dashes
                    var sanitizedAreaName = System.Text.RegularExpressions.Regex.Replace(
                        area.Name.ToLower(),
                        @"[\s/\\:*?""<>|]+",
                        "-"
                    );
                    // Remove leading/trailing dashes and collapse multiple dashes
                    sanitizedAreaName = System.Text.RegularExpressions.Regex.Replace(
                        sanitizedAreaName.Trim('-'),
                        @"-+",
                        "-"
                    );

                    // Ensure task subdirectory exists
                    var taskDir = Path.Combine(_outputPath, "tasks");
                    if (!Directory.Exists(taskDir))
                    {
                        Directory.CreateDirectory(taskDir);
                    }

                    var areaOutputPath = Path.Combine(taskDir, $"{sanitizedAreaName}-tasks.md");

                    // Load existing tracker if file exists to preserve task statuses
                    var tracker = new TaskTracker();
                    var areaFileExists = File.Exists(areaOutputPath);
                    if (areaFileExists)
                    {
                        tracker.LoadFromFile(areaOutputPath);
                    }

                    var added = 0;
                    foreach (var task in area.Tasks.OrderBy(t => t.DependencyLevel))
                    {
                        // Skip tasks that already exist - preserve their current status
                        if (existingTaskIds.Contains(task.Id) || doneTaskNames.Contains(NormalizeTaskName(task.Name)))
                        {
                            continue;
                        }
                        existingTaskIds.Add(task.Id);
                        added++;

                        // Format: [task-id] Task name: Description
                        var deps = task.Dependencies.Any()
                            ? $" (depends on: {string.Join(", ", task.Dependencies)})"
                            : "";
                        var taskDescription = $"[{task.Id}] {task.Name}{deps}";

                        // Parse priority from string to enum
                        var priority = ParsePriority(task.Priority);
                        var taskRecord = tracker.AddTask(taskDescription, priority);
                        taskRecord.FeatureId = task.FeatureId;

                        // Capture specification version for drift detection
                        if (_specification != null)
                        {
                            taskRecord.SpecificationVersion = _specification.Version;
                            taskRecord.SpecificationContentHash = _specification.ContentHash;
                        }

                        // Set the recommended agent type if available (normalized to valid agent type)
                        if (!string.IsNullOrEmpty(task.AgentType))
                        {
                            var normalizedAgentType = AgentTypeValidator.Normalize(task.AgentType);
                            tracker.UpdateTask(taskRecord, TaskStatus.Unassigned, normalizedAgentType);
                        }
                    }

                    // An area whose tasks all exist already adds no new file — it is done, not pending
                    if (added == 0 && !areaFileExists)
                    {
                        _areasWithoutNewTasks.Add(area.Name);
                        continue;
                    }

                    // Save the tracker with all individual tasks
                    tracker.SaveToFile(areaOutputPath, $"KoboldLair {area.Name} Tasks");

                    // Attached only now, so rows are written once with their final agent type and spec version.
                    if (TaskRepository != null && !string.IsNullOrEmpty(ProjectId))
                    {
                        tracker.Repository = TaskRepository;
                        tracker.ProjectId = ProjectId;
                        tracker.AreaName = sanitizedAreaName;
                        try
                        {
                            await tracker.EnsureInRepositoryAsync();
                        }
                        catch (Exception dbEx)
                        {
                            // The task file is the source of truth here; a database failure must not fail the area.
                            _logger?.LogError(dbEx, "Failed to write tasks of area {Area} to the database", area.Name);
                        }
                    }

                    taskFiles[area.Name] = areaOutputPath;
                }
                catch (Exception ex)
                {
                    // Track failed areas for reprocessing with error details
                    System.Diagnostics.Debug.WriteLine($"Failed to create task file for area '{area.Name}': {ex.Message}");
                    failedAreas.Add(area.Name);
                }
            }

            return (taskFiles, failedAreas);
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

        /// <summary>
        /// Parses priority string from WyvernTask to TaskPriority enum
        /// </summary>
        private static TaskPriority ParsePriority(string priority)
        {
            return priority?.ToLower() switch
            {
                "critical" => TaskPriority.Critical,
                "high" => TaskPriority.High,
                "low" => TaskPriority.Low,
                _ => TaskPriority.Normal
            };
        }

        /// <summary>
        /// Validates task dependencies exist and removes invalid ones.
        /// Logs warnings for tasks with non-existent dependencies.
        /// </summary>
        private void ValidateAndFixTaskDependencies(WyvernAnalysis analysis)
        {
            // Build a set of all valid task IDs across all areas — plus tasks already written by an earlier analysis,
            // which a re-analysis's new tasks may depend on without re-emitting them
            var validTaskIds = new HashSet<string>(
                LoadExistingTaskSummaries(_outputPath).Select(t => t.Id), StringComparer.OrdinalIgnoreCase);
            foreach (var area in analysis.Areas)
            {
                foreach (var task in area.Tasks)
                {
                    validTaskIds.Add(task.Id);
                }
            }

            // Check each task's dependencies - remove invalid ones
            foreach (var area in analysis.Areas)
            {
                foreach (var task in area.Tasks)
                {
                    if (task.Dependencies.Count == 0)
                        continue;

                    var invalidDeps = new List<string>();
                    foreach (var dep in task.Dependencies)
                    {
                        if (!validTaskIds.Contains(dep))
                        {
                            invalidDeps.Add(dep);
                        }
                    }

                    if (invalidDeps.Count > 0)
                    {
                        _logger?.LogWarning(
                            "Task [{TaskId}] has invalid dependencies: {InvalidDeps}. These will be removed.",
                            task.Id,
                            string.Join(", ", invalidDeps));

                        // Remove invalid dependencies
                        task.Dependencies.RemoveAll(d => invalidDeps.Contains(d, StringComparer.OrdinalIgnoreCase));
                    }
                }
            }

            // Detect and break circular dependencies
            DetectAndBreakCycles(analysis);
        }

        /// <summary>
        /// Detects circular dependencies between tasks and breaks them by removing the
        /// dependency edge that creates the cycle (from the task with higher dependency level).
        /// Uses depth-first search with coloring to detect back-edges.
        /// </summary>
        private void DetectAndBreakCycles(WyvernAnalysis analysis)
        {
            // Build adjacency map: taskId → list of dependency taskIds
            var allTasks = new Dictionary<string, WyvernTask>(StringComparer.OrdinalIgnoreCase);
            foreach (var area in analysis.Areas)
            {
                foreach (var task in area.Tasks)
                {
                    allTasks[task.Id] = task;
                }
            }

            // DFS cycle detection with coloring: White=unvisited, Gray=in-stack, Black=done
            var color = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase); // 0=white, 1=gray, 2=black
            var cycleEdges = new List<(string From, string To)>();

            foreach (var taskId in allTasks.Keys)
                color[taskId] = 0;

            foreach (var taskId in allTasks.Keys)
            {
                if (color[taskId] == 0)
                    DfsCycleDetect(taskId, allTasks, color, cycleEdges);
            }

            // Break detected cycles by removing the back-edge dependency
            foreach (var (from, to) in cycleEdges)
            {
                if (allTasks.TryGetValue(from, out var task))
                {
                    task.Dependencies.RemoveAll(d => d.Equals(to, StringComparison.OrdinalIgnoreCase));
                    _logger?.LogWarning(
                        "Circular dependency detected: [{From}] → [{To}]. Removed dependency to break cycle.",
                        from, to);
                }
            }
        }

        private void DfsCycleDetect(
            string taskId,
            Dictionary<string, WyvernTask> allTasks,
            Dictionary<string, int> color,
            List<(string From, string To)> cycleEdges)
        {
            color[taskId] = 1; // Gray - in stack

            if (allTasks.TryGetValue(taskId, out var task))
            {
                foreach (var dep in task.Dependencies.ToList())
                {
                    if (!color.ContainsKey(dep))
                        continue; // Unknown task, already cleaned up

                    if (color[dep] == 1)
                    {
                        // Back-edge found: dep is in our DFS stack → cycle
                        cycleEdges.Add((taskId, dep));
                    }
                    else if (color[dep] == 0)
                    {
                        DfsCycleDetect(dep, allTasks, color, cycleEdges);
                    }
                }
            }

            color[taskId] = 2; // Black - done
        }

        public string ProjectName => _projectName;
        public string SpecificationPath => _specificationPath;

        /// <summary>
        /// Performs targeted task refinement in response to an escalation.
        /// Returns a result describing what action to take (split, add dependency, etc.)
        /// without modifying task files directly — Drake applies the changes.
        /// </summary>
        public async Task<WyvernRefinementResult> RefineTaskAsync(
            string taskId,
            string area,
            EscalationAlert escalation,
            List<ReflectionEntry> reflections)
        {
            var result = new WyvernRefinementResult();

            try
            {
                // Build a targeted refinement prompt
                var prompt = new System.Text.StringBuilder();
                prompt.AppendLine("# Task Refinement Request");
                prompt.AppendLine();
                prompt.AppendLine($"## Escalation: {escalation.Type}");
                prompt.AppendLine($"Task ID: {taskId}");
                prompt.AppendLine($"Area: {area}");
                prompt.AppendLine($"Summary: {escalation.Summary}");
                prompt.AppendLine($"Agent Type: {escalation.AgentType}");
                prompt.AppendLine();

                if (reflections.Any())
                {
                    prompt.AppendLine("## Reflection History");
                    foreach (var r in reflections.TakeLast(5))
                    {
                        prompt.AppendLine($"- progress={r.ProgressPercent}%, confidence={r.ConfidencePercent}%, decision={r.Decision}");
                        if (!string.IsNullOrEmpty(r.Blockers))
                            prompt.AppendLine($"  Blockers: {r.Blockers}");
                    }
                    prompt.AppendLine();
                }

                // Load specification for context
                string? specContent = null;
                if (File.Exists(_specificationPath))
                {
                    specContent = await File.ReadAllTextAsync(_specificationPath);
                }

                if (!string.IsNullOrEmpty(specContent))
                {
                    prompt.AppendLine("## Specification (excerpt)");
                    // Truncate to keep prompt manageable
                    var truncated = specContent.Length > 3000 ? specContent[..3000] + "\n..." : specContent;
                    prompt.AppendLine(truncated);
                    prompt.AppendLine();
                }

                prompt.AppendLine("## Decision Required");
                prompt.AppendLine("Based on the escalation type, decide the best action.");
                prompt.AppendLine("Return ONLY a JSON object with: { \"action\": \"split|add_dependency|revise|no_change\", \"summary\": \"what to do\" }");

                switch (escalation.Type)
                {
                    case EscalationType.NeedsSplit:
                        prompt.AppendLine("- This task is too large. How should it be split into smaller subtasks?");
                        result.Action = RefinementAction.Split;
                        break;

                    case EscalationType.MissingDependency:
                        prompt.AppendLine("- What dependency is missing and needs to be added?");
                        result.Action = RefinementAction.AddDependency;
                        break;

                    case EscalationType.TaskInfeasible:
                        prompt.AppendLine("- Can this task be revised to be feasible, or should it be removed?");
                        result.Action = RefinementAction.ReviseComplexity;
                        break;

                    default:
                        result.Action = RefinementAction.NoChange;
                        result.Summary = $"No refinement action for escalation type: {escalation.Type}";
                        _logger?.LogInformation(
                            "Wyvern refinement skipped for task {TaskId}: {Summary}",
                            taskId, result.Summary);
                        return result;
                }

                // Send to LLM for actual analysis
                var messages = await _analyzerAgent.RunAsync(prompt.ToString(), maxIterations: 1);
                var lastMessage = messages.LastOrDefault(m => m.Role == "assistant");
                var responseText = OrchestratorAgent.ExtractTextFromContent(lastMessage?.Content);

                if (!string.IsNullOrEmpty(responseText))
                {
                    result.Summary = $"Task {taskId} [{result.Action}]: {responseText.Length switch
                    {
                        > 200 => responseText[..200] + "...",
                        _ => responseText
                    }}";
                }
                else
                {
                    result.Summary = $"Task {taskId} [{result.Action}]: LLM returned no refinement guidance";
                }

                _logger?.LogInformation(
                    "Wyvern refinement for task {TaskId}: {Action} - {Summary}",
                    taskId, result.Action, result.Summary);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during Wyvern refinement for task {TaskId}", taskId);
                result.Action = RefinementAction.NoChange;
                result.Summary = $"Refinement failed: {ex.Message}";
            }

            return result;
        }
    }
}
