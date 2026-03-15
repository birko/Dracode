using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using DraCode.KoboldLair.Factories;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services
{
    public class WyrmProcessingService : PeriodicBackgroundService
    {
        private readonly ILogger<WyrmProcessingService> _logger;
        private readonly ProjectService _projectService;
        private readonly WyrmFactory _wyrmFactory;
        private readonly SharedPlanningContextService? _sharedPlanningContext;
        private readonly SemaphoreSlim _projectThrottle;
        private const int MaxConcurrentProjects = 5;

        protected override ILogger Logger => _logger;

        public WyrmProcessingService(
            ILogger<WyrmProcessingService> logger,
            ProjectService projectService,
            WyrmFactory wyrmFactory,
            SharedPlanningContextService? sharedPlanningContext = null,
            int checkIntervalSeconds = 60)
            : base(TimeSpan.FromSeconds(checkIntervalSeconds))
        {
            _logger = logger;
            _projectService = projectService;
            _wyrmFactory = wyrmFactory;
            _sharedPlanningContext = sharedPlanningContext;
            _projectThrottle = new SemaphoreSlim(MaxConcurrentProjects, MaxConcurrentProjects);
        }

        protected override async Task ExecuteCycleAsync(CancellationToken stoppingToken)
        {
            var projects = _projectService.GetProjectsByStatus(ProjectStatus.New)
                .Where(p => p.ExecutionState == ProjectExecutionState.Running)
                .ToList();

            if (!projects.Any()) return;

            var skipped = _projectService.GetProjectsByStatus(ProjectStatus.New)
                .Where(p => p.ExecutionState != ProjectExecutionState.Running)
                .ToList();
            foreach (var p in skipped)
            {
                _logger.LogDebug("Skipping project {ProjectName} - execution state: {State}", p.Name, p.ExecutionState);
            }

            await Task.WhenAll(projects.Select(p => RunWyrmAsync(p, stoppingToken)));
        }

        private async Task RunWyrmAsync(Project project, CancellationToken stoppingToken)
        {
            await _projectThrottle.WaitAsync(stoppingToken);
            try
            {
                _logger.LogInformation("[Wyrm] START {ProjectName} | Starting pre-analysis", project.Name);

                var wyrmStart = DateTime.UtcNow;

                var wyrmAgent = _wyrmFactory.CreateWyrmPreAnalysisAgent(project);
                var spec = File.Exists(project.Paths.Specification)
                    ? await File.ReadAllTextAsync(project.Paths.Specification, stoppingToken)
                    : "";

                // GAP 2 FIX: Build cross-project insights context
                var crossProjectContext = await BuildCrossProjectContextAsync(project.Id);

                // Try to parse Wyrm's response
                WyrmRecommendation? rec = null;
                try
                {
                    var jsonText = await wyrmAgent.AnalyzeSpecificationAsync(spec, crossProjectContext);
                    rec = JsonSerializer.Deserialize<WyrmRecommendation>(jsonText, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (rec != null)
                    {
                        rec.ProjectId = project.Id;
                        rec.ProjectName = project.Name;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse Wyrm response for {Name}, using fallback", project.Name);
                }
                
                // Fallback to default values if parsing failed
                rec ??= new WyrmRecommendation { 
                    ProjectId = project.Id,
                    ProjectName = project.Name,
                    RecommendedLanguages = new List<string> { "general" }, 
                    RecommendedAgentTypes = new Dictionary<string, string> { { "general", "coding" } }, 
                    TechnicalStack = new List<string>(), 
                    SuggestedAreas = new List<string> { "general" }, 
                    Complexity = "Medium" 
                };

                var path = Path.Combine(project.Paths.Output, "wyrm-recommendation.json");
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(rec, new JsonSerializerOptions {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                }), stoppingToken);

                var wyrmDuration = DateTime.UtcNow - wyrmStart;

                _projectService.UpdateProjectStatus(project.Id, ProjectStatus.WyrmAssigned);
                _logger.LogInformation("[Wyrm] COMPLETE {ProjectName} | Duration: {Duration}ms", project.Name, wyrmDuration.TotalMilliseconds.ToString("F0"));

                // Warn if Wyrm took too long
                if (wyrmDuration.TotalSeconds > 60)
                {
                    _logger.LogWarning("[Wyrm] SLOW {ProjectName} | Pre-analysis took {Duration}s",
                        project.Name, wyrmDuration.TotalSeconds.ToString("F1"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Wyrm] FAILED {ProjectName} | Analysis failed", project.Name);
            }
            finally { _projectThrottle.Release(); }
        }


        /// <summary>
        /// Builds cross-project learning context for Wyrm analysis (GAP 2 FIX)
        /// </summary>
        private async Task<string> BuildCrossProjectContextAsync(string projectId)
        {
            if (_sharedPlanningContext == null)
            {
                return string.Empty;
            }

            var sb = new System.Text.StringBuilder();

            try
            {
                // Get insights from other successful projects
                var codingInsights = await _sharedPlanningContext.GetCrossProjectInsightsAsync(projectId, "coding", 5);
                var csharpInsights = await _sharedPlanningContext.GetCrossProjectInsightsAsync(projectId, "csharp", 3);
                var jsInsights = await _sharedPlanningContext.GetCrossProjectInsightsAsync(projectId, "javascript", 3);
                var pythonInsights = await _sharedPlanningContext.GetCrossProjectInsightsAsync(projectId, "python", 3);

                var allInsights = codingInsights
                    .Concat(csharpInsights)
                    .Concat(jsInsights)
                    .Concat(pythonInsights)
                    .GroupBy(i => i.AgentType)
                    .ToList();

                if (!allInsights.Any())
                {
                    return string.Empty;
                }

                sb.AppendLine();
                sb.AppendLine("## Cross-Project Learning Context");
                sb.AppendLine();
                sb.AppendLine("Based on previous successful projects, here are patterns to consider:");
                sb.AppendLine();

                foreach (var group in allInsights)
                {
                    var agentType = group.Key;
                    var insights = group.ToList();
                    var avgSteps = insights.Average(i => i.StepCount);
                    var avgDuration = insights.Average(i => i.DurationSeconds);
                    var successRate = insights.Count(i => i.Success) / (double)insights.Count * 100;

                    sb.AppendLine($"**{agentType} tasks**: {insights.Count} completed, avg {avgSteps:F1} steps, {avgDuration:F0}s duration, {successRate:F0}% success rate");
                }

                sb.AppendLine();
                sb.AppendLine("Consider these patterns when recommending agent types and estimating complexity.");
                sb.AppendLine();

                _logger.LogDebug("Added cross-project context with {Count} insight groups for project {Name}",
                    allInsights.Count, projectId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to build cross-project context, proceeding without it");
            }

            return sb.ToString();
        }

    }
}
