using DraCode.KoboldLair.Services;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services.CommandHandlers
{
    public class OperationsCommandHandler
    {
        private readonly ILogger _logger;
        private readonly ProjectService _projectService;
        private readonly DragonRequestQueue _dragonRequestQueue;

        public OperationsCommandHandler(
            ILogger logger,
            ProjectService projectService,
            DragonRequestQueue dragonRequestQueue)
        {
            _logger = logger;
            _projectService = projectService;
            _dragonRequestQueue = dragonRequestQueue;
        }

        public Task<object> RetryAnalysisAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var success = _projectService.RetryAnalysis(projectId!);

            if (!success)
            {
                var project = _projectService.GetProject(projectId!)
                    ?? throw new InvalidOperationException($"Project not found: {projectId}");
                throw new InvalidOperationException($"Cannot retry analysis - project status is {project.Status}, not Failed");
            }

            return Task.FromResult<object>(new { success = true, message = "Analysis retry initiated. Project will be reprocessed shortly." });
        }

        public async Task<object> CancelDragonRequestAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var sessionId = data.Value.TryGetProperty("sessionId", out var sessionIdElement)
                ? sessionIdElement.GetString()
                : null;

            if (string.IsNullOrEmpty(sessionId))
                throw new InvalidOperationException("sessionId is required for cancel_dragon_request");

            var cancelled = await _dragonRequestQueue.CancelSessionRequestAsync(sessionId!);

            return new
            {
                success = cancelled,
                message = cancelled
                    ? $"Dragon request for session {sessionId} has been cancelled"
                    : $"No active request found for session {sessionId}",
                sessionId,
                timestamp = DateTime.UtcNow
            };
        }

        public async Task<object> GetImplementationSummaryAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectService.GetProject(projectId!)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            var summaryPath = Path.Combine(project.Paths.Output ?? "", "implementation-summary.json");
            if (!Path.IsPathRooted(summaryPath))
                summaryPath = Path.Combine(_projectService.ProjectsPath, summaryPath);

            if (!File.Exists(summaryPath))
            {
                return new
                {
                    exists = false,
                    message = "No implementation summary found. Complete some tasks to generate impact tracking data."
                };
            }

            try
            {
                var json = await File.ReadAllTextAsync(summaryPath);
                var summary = JsonSerializer.Deserialize<object>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return new { exists = true, summary };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read implementation summary from {Path}", summaryPath);
                return new
                {
                    exists = false,
                    message = $"Failed to read implementation summary: {ex.Message}"
                };
            }
        }
    }
}
