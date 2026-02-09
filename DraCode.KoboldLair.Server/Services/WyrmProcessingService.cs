using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using DraCode.KoboldLair.Factories;
using DraCode.Agent;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services
{
    public class WyrmProcessingService : BackgroundService
    {
        private readonly ILogger<WyrmProcessingService> _logger;
        private readonly ProjectService _projectService;
        private readonly WyrmFactory _wyrmFactory;
        private readonly TimeSpan _checkInterval;
        private bool _isRunning;
        private readonly object _lock = new object();
        private readonly SemaphoreSlim _projectThrottle;
        private const int MaxConcurrentProjects = 5;

        public WyrmProcessingService(
            ILogger<WyrmProcessingService> logger,
            ProjectService projectService,
            WyrmFactory wyrmFactory,
            int checkIntervalSeconds = 60)
        {
            _logger = logger;
            _projectService = projectService;
            _wyrmFactory = wyrmFactory;
            _checkInterval = TimeSpan.FromSeconds(checkIntervalSeconds);
            _isRunning = false;
            _projectThrottle = new SemaphoreSlim(MaxConcurrentProjects, MaxConcurrentProjects);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Wyrm Processing Service started");
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                    bool canRun;
                    lock (_lock) { canRun = !_isRunning; if (canRun) _isRunning = true; }
                    if (!canRun) continue;
                    await ProcessProjectsAsync(stoppingToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { _logger.LogError(ex, "Error in Wyrm processing"); }
                finally { lock (_lock) { _isRunning = false; } }
            }
        }

        private async Task ProcessProjectsAsync(CancellationToken stoppingToken)
        {
            var projects = _projectService.GetProjectsByStatus(ProjectStatus.New);
            if (!projects.Any()) return;
            await Task.WhenAll(projects.Select(p => RunWyrmAsync(p, stoppingToken)));
        }

        private async Task RunWyrmAsync(Project project, CancellationToken stoppingToken)
        {
            await _projectThrottle.WaitAsync(stoppingToken);
            try
            {
                _logger.LogInformation("Starting Wyrm analysis: {Name}", project.Name);
                var wyrm = _wyrmFactory.CreateWyrm(project);
                var spec = File.Exists(project.Paths.Specification)
                    ? await File.ReadAllTextAsync(project.Paths.Specification, stoppingToken)
                    : "";
                
                var prompt = $@"Analyze the following project specification and provide initial recommendations as JSON:
{spec}

Provide JSON with: RecommendedLanguages[], RecommendedAgentTypes{{}}, TechnicalStack[], SuggestedAreas[], Complexity";
                
                var messages = await wyrm.RunAsync(prompt);
                var response = messages.LastOrDefault()?.Content ?? "";
                
                var rec = new WyrmRecommendation { 
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
                
                _projectService.UpdateProjectStatus(project.Id, ProjectStatus.WyrmAssigned);
                _logger.LogInformation("Wyrm analysis complete: {Name}", project.Name);
            }
            catch (Exception ex) { _logger.LogError(ex, "Wyrm analysis failed: {Name}", project.Name); }
            finally { _projectThrottle.Release(); }
        }
    }
}
