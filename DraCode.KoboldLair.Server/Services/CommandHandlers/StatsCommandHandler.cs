using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services.CommandHandlers
{
    public class StatsCommandHandler
    {
        private readonly ProjectService _projectService;
        private readonly DragonService _dragonService;
        private readonly DrakeFactory _drakeFactory;
        private readonly WyvernFactory _wyvernFactory;

        public StatsCommandHandler(
            ProjectService projectService,
            DragonService dragonService,
            DrakeFactory drakeFactory,
            WyvernFactory wyvernFactory)
        {
            _projectService = projectService;
            _dragonService = dragonService;
            _drakeFactory = drakeFactory;
            _wyvernFactory = wyvernFactory;
        }

        public async Task<object> GetHierarchyAsync()
        {
            var projectsTask = Task.Run(() => _projectService.GetAllProjects());
            var statsTask = Task.Run(() => _projectService.GetStatistics());
            var dragonStatsTask = Task.Run(() => _dragonService.GetStatistics());
            await Task.WhenAll(projectsTask, statsTask, dragonStatsTask);
            var projects = projectsTask.Result;
            var stats = statsTask.Result;
            var dragonStats = dragonStatsTask.Result;

            var drakes = _drakeFactory.GetAllDrakes();
            var totalKobolds = drakes.Sum(d => d.GetStatistics().WorkingKobolds);
            var totalWyverns = _wyvernFactory.TotalWyverns;

            var projectTasks = projects
                .Where(p => p.Tracking.WyvernId != null)
                .Select(p => Task.Run(() =>
                {
                    var wyvern = _wyvernFactory.GetWyvern(p.Name);
                    return new
                    {
                        id = p.Id,
                        name = p.Name,
                        icon = "📁",
                        status = p.Status.ToString().ToLower(),
                        wyvern = wyvern != null ? new
                        {
                            id = p.Tracking.WyvernId,
                            name = $"wyvern ({p.Name})",
                            icon = "🐲",
                            status = p.Status == ProjectStatus.Analyzed ? "active" : "working",
                            analyzed = p.Status >= ProjectStatus.Analyzed,
                            totalTasks = wyvern.Analysis?.TotalTasks ?? 0
                        } : (object?)null
                    };
                }));

            var projectHierarchies = await Task.WhenAll(projectTasks);

            return new
            {
                statistics = new
                {
                    dragonSessions = dragonStats.ActiveSessions,
                    projects = stats.TotalProjects,
                    wyrms = totalWyverns,
                    drakes = drakes.Count,
                    koboldsWorking = totalKobolds
                },
                projects = projects.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Status,
                    WyvernId = p.Tracking.WyvernId,
                    CreatedAt = p.Timestamps.CreatedAt,
                    AnalyzedAt = p.Timestamps.AnalyzedAt,
                    OutputPath = p.Paths.Output,
                    SpecificationPath = p.Paths.Specification,
                    TaskFiles = p.Paths.TaskFiles,
                    ErrorMessage = p.Tracking.ErrorMessage
                }),
                hierarchy = new
                {
                    dragon = new
                    {
                        name = "Dragon Requirements Agent",
                        icon = "🐉",
                        status = dragonStats.ActiveSessions > 0 ? "active" : "idle",
                        activeSessions = dragonStats.ActiveSessions
                    },
                    projects = projectHierarchies
                }
            };
        }

        public Task<object> GetProjectsAsync()
        {
            var projects = _projectService.GetAllProjects();
            return Task.FromResult<object>(projects);
        }

        public Task<object> GetProjectAgentsAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectService.GetProject(projectId!)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            // Get all drakes for this project (there can be multiple, one per feature/task file)
            var drakesForProject = _drakeFactory.GetDrakesForProject(project.Id);
            var totalKobolds = drakesForProject.Sum(d => d.Drake.GetStatistics().WorkingKobolds);

            // Check if wyvern exists for this project (by project name)
            var wyvern = _wyvernFactory.GetWyvern(project.Name);

            return Task.FromResult<object>(new
            {
                projectId,
                projectName = project.Name,
                agents = new
                {
                    wyverns = wyvern != null ? 1 : 0,
                    drakes = drakesForProject.Count,
                    kobolds = totalKobolds
                }
            });
        }

        public Task<object> GetStatsAsync()
        {
            var projectStats = _projectService.GetStatistics();
            var dragonStats = _dragonService.GetStatistics();
            var drakes = _drakeFactory.GetAllDrakes();

            return Task.FromResult<object>(new
            {
                projects = projectStats,
                dragon = dragonStats,
                drakes = drakes.Count,
                wyrms = _wyvernFactory.TotalWyverns,
                koboldsWorking = drakes.Sum(d => d.GetStatistics().WorkingKobolds)
            });
        }
    }
}
