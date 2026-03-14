using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Server.Services.CommandHandlers;
using DraCode.KoboldLair.Services;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Routes WebSocket commands to specialized handler classes.
    /// </summary>
    public class WebSocketCommandHandler
    {
        private static readonly JsonSerializerOptions s_camelCaseOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        private static readonly JsonSerializerOptions s_writeOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly ILogger<WebSocketCommandHandler> _logger;
        private readonly StatsCommandHandler _stats;
        private readonly ProviderCommandHandler _providers;
        private readonly ProjectConfigCommandHandler _projectConfig;
        private readonly OperationsCommandHandler _operations;

        public WebSocketCommandHandler(
            ILogger<WebSocketCommandHandler> logger,
            ProjectService projectService,
            DragonService dragonService,
            ProviderConfigurationService providerConfigService,
            ProjectRepository projectRepository,
            DrakeFactory drakeFactory,
            WyvernFactory wyvernFactory,
            DragonRequestQueue? dragonRequestQueue = null)
        {
            _logger = logger;

            _stats = new StatsCommandHandler(projectService, dragonService, drakeFactory, wyvernFactory);
            _providers = new ProviderCommandHandler(providerConfigService);
            _projectConfig = new ProjectConfigCommandHandler(projectService, providerConfigService, projectRepository);
            _operations = new OperationsCommandHandler(
                logger,
                projectService,
                dragonRequestQueue ?? throw new ArgumentNullException(nameof(dragonRequestQueue)));
        }

        public async Task HandleCommandAsync(WebSocket webSocket, string messageText)
        {
            try
            {
                _logger.LogDebug("Received command message: {Message}", messageText);
                var message = JsonSerializer.Deserialize<WebSocketCommand>(messageText, s_camelCaseOptions);
                if (message == null)
                {
                    await SendErrorAsync(webSocket, null, "Invalid message format");
                    return;
                }

                _logger.LogInformation("Processing command: {Command} (ID: {RequestId})", message.Command, message.Id);

                object? responseData = message.Command?.ToLowerInvariant() switch
                {
                    // Stats & hierarchy
                    "get_hierarchy" => await _stats.GetHierarchyAsync(),
                    "get_projects" => await _stats.GetProjectsAsync(),
                    "get_project_agents" => await _stats.GetProjectAgentsAsync(message.Data),
                    "get_stats" => await _stats.GetStatsAsync(),

                    // Provider configuration
                    "get_providers" => await _providers.GetProvidersAsync(),
                    "configure_provider" => await _providers.ConfigureProviderAsync(message.Data),
                    "validate_provider" => await _providers.ValidateProviderAsync(message.Data),
                    "get_providers_for_agent" => await _providers.GetProvidersForAgentAsync(message.Data),

                    // Project configuration
                    "get_project_config" => await _projectConfig.GetProjectConfigAsync(message.Data),
                    "update_project_config" => await _projectConfig.UpdateProjectConfigAsync(message.Data),
                    "get_project_providers" => await _projectConfig.GetProjectProvidersAsync(message.Data),
                    "update_project_providers" => await _projectConfig.UpdateProjectProvidersAsync(message.Data),
                    "toggle_agent" => await _projectConfig.ToggleAgentAsync(message.Data),
                    "get_agent_status" => await _projectConfig.GetAgentStatusAsync(message.Data),
                    "get_all_project_configs" => await _projectConfig.GetAllProjectConfigsAsync(),
                    "get_project_config_full" => await _projectConfig.GetProjectConfigFullAsync(message.Data),
                    "update_project_config_full" => await _projectConfig.UpdateProjectConfigFullAsync(message.Data),
                    "delete_project_config" => await _projectConfig.DeleteProjectConfigAsync(message.Data),
                    "get_agent_config" => await _projectConfig.GetAgentConfigAsync(message.Data),
                    "update_agent_config" => await _projectConfig.UpdateAgentConfigAsync(message.Data),

                    // Operations
                    "retry_analysis" => await _operations.RetryAnalysisAsync(message.Data),
                    "cancel_dragon_request" => await _operations.CancelDragonRequestAsync(message.Data),
                    "get_implementation_summary" => await _operations.GetImplementationSummaryAsync(message.Data),

                    _ => throw new InvalidOperationException($"Unknown command: {message.Command}")
                };

                await SendResponseAsync(webSocket, message.Id, responseData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling command");
                await SendErrorAsync(webSocket, null, ex.Message);
            }
        }

        private async Task SendResponseAsync(WebSocket webSocket, string? requestId, object? data)
        {
            if (webSocket.State != WebSocketState.Open) return;

            var response = new
            {
                id = requestId,
                type = "response",
                data,
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(response, s_writeOptions);
            _logger.LogDebug("Sending response: {Response}", json);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        private async Task SendErrorAsync(WebSocket webSocket, string? requestId, string error)
        {
            if (webSocket.State != WebSocketState.Open) return;

            var response = new
            {
                id = requestId,
                type = "error",
                error,
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(response, s_writeOptions);
            _logger.LogWarning("Sending error: {Error}", json);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
