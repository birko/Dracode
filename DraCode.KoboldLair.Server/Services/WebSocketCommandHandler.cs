using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Server.Models.Configuration;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Unified WebSocket command handler for all KoboldLair operations
    /// </summary>
    public class WebSocketCommandHandler
    {
        private readonly ILogger<WebSocketCommandHandler> _logger;
        private readonly ProjectService _projectService;
        private readonly DragonService _dragonService;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ProjectConfigurationService _projectConfigService;
        private readonly DrakeFactory _drakeFactory;
        private readonly WyvernFactory _wyvernFactory;

        public WebSocketCommandHandler(
            ILogger<WebSocketCommandHandler> logger,
            ProjectService projectService,
            DragonService dragonService,
            ProviderConfigurationService providerConfigService,
            ProjectConfigurationService projectConfigService,
            DrakeFactory drakeFactory,
            WyvernFactory wyvernFactory)
        {
            _logger = logger;
            _projectService = projectService;
            _dragonService = dragonService;
            _providerConfigService = providerConfigService;
            _projectConfigService = projectConfigService;
            _drakeFactory = drakeFactory;
            _wyvernFactory = wyvernFactory;
        }

        public async Task HandleCommandAsync(WebSocket webSocket, string messageText)
        {
            try
            {
                _logger.LogDebug("Received command message: {Message}", messageText);
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                };
                var message = JsonSerializer.Deserialize<WebSocketCommand>(messageText, options);
                if (message == null)
                {
                    await SendErrorAsync(webSocket, null, "Invalid message format");
                    return;
                }

                _logger.LogInformation("Processing command: {Command} (ID: {RequestId})", message.Command, message.Id);

                object? responseData = message.Command?.ToLowerInvariant() switch
                {
                    "get_hierarchy" => await GetHierarchyAsync(),
                    "get_projects" => await GetProjectsAsync(),
                    "get_stats" => await GetStatsAsync(),
                    "get_providers" => await GetProvidersAsync(),
                    "configure_provider" => await ConfigureProviderAsync(message.Data),
                    "validate_provider" => await ValidateProviderAsync(message.Data),
                    "get_providers_for_agent" => await GetProvidersForAgentAsync(message.Data),
                    "get_project_config" => await GetProjectConfigAsync(message.Data),
                    "update_project_config" => await UpdateProjectConfigAsync(message.Data),
                    "get_project_providers" => await GetProjectProvidersAsync(message.Data),
                    "update_project_providers" => await UpdateProjectProvidersAsync(message.Data),
                    "toggle_agent" => await ToggleAgentAsync(message.Data),
                    "get_agent_status" => await GetAgentStatusAsync(message.Data),
                    "get_all_project_configs" => await GetAllProjectConfigsAsync(),
                    "get_project_config_full" => await GetProjectConfigFullAsync(message.Data),
                    "update_project_config_full" => await UpdateProjectConfigFullAsync(message.Data),
                    "delete_project_config" => await DeleteProjectConfigAsync(message.Data),
                    "get_agent_config" => await GetAgentConfigAsync(message.Data),
                    "update_agent_config" => await UpdateAgentConfigAsync(message.Data),
                    "retry_analysis" => await RetryAnalysisAsync(message.Data),
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

        private async Task<object> GetHierarchyAsync()
        {
            var projects = _projectService.GetAllProjects();
            var stats = _projectService.GetStatistics();
            var dragonStats = _dragonService.GetStatistics();
            var drakes = _drakeFactory.GetAllDrakes();
            var totalKobolds = drakes.Sum(d => d.GetStatistics().WorkingKobolds);
            var TotalWyverns = _wyvernFactory.TotalWyverns;

            return new
            {
                statistics = new
                {
                    dragonSessions = dragonStats.ActiveSessions,
                    projects = stats.TotalProjects,
                    wyrms = TotalWyverns,
                    drakes = drakes.Count,
                    koboldsWorking = totalKobolds
                },
                projects = projects.Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Status,
                    p.WyvernId,
                    p.CreatedAt,
                    p.AnalyzedAt,
                    p.OutputPath,
                    p.SpecificationPath,
                    p.TaskFiles,
                    p.ErrorMessage
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
                    projects = projects.Where(p => p.WyvernId != null).Select(p =>
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
                                id = p.WyvernId,
                                name = $"wyvern ({p.Name})",
                                icon = "🐲",
                                status = p.Status == ProjectStatus.Analyzed ? "active" : "working",
                                analyzed = p.Status >= ProjectStatus.Analyzed,
                                totalTasks = wyvern.Analysis?.TotalTasks ?? 0
                            } : null
                        };
                    }).ToList()
                }
            };
        }

        private async Task<object> GetProjectsAsync()
        {
            var projects = _projectService.GetAllProjects();
            return projects;
        }

        private async Task<object> GetStatsAsync()
        {
            var projectStats = _projectService.GetStatistics();
            var dragonStats = _dragonService.GetStatistics();
            var drakes = _drakeFactory.GetAllDrakes();

            return new
            {
                projects = projectStats,
                dragon = dragonStats,
                drakes = drakes.Count,
                wyrms = _wyvernFactory.TotalWyverns,
                koboldsWorking = drakes.Sum(d => d.GetStatistics().WorkingKobolds)
            };
        }

        private async Task<object> GetProvidersAsync()
        {
            var providers = _providerConfigService.GetAllProviders().Select(p => new
            {
                p.Name,
                p.DisplayName,
                p.Type,
                p.DefaultModel,
                p.CompatibleAgents,
                p.IsEnabled,
                p.RequiresApiKey,
                p.Description,
                IsConfigured = _providerConfigService.ValidateProvider(p.Name).isValid
            });

            var userSettings = _providerConfigService.GetUserSettings();

            return new
            {
                providers,
                defaultProvider = _providerConfigService.GetDefaultProvider(),
                agentProviders = new
                {
                    dragonProvider = userSettings.DragonProvider,
                    wyrmProvider = userSettings.WyrmProvider,
                    wyvernProvider = userSettings.WyvernProvider,
                    koboldProvider = userSettings.KoboldProvider,
                    dragonModel = userSettings.DragonModel,
                    wyrmModel = userSettings.WyrmModel,
                    wyvernModel = userSettings.WyvernModel,
                    koboldModel = userSettings.KoboldModel
                }
            };
        }

        private async Task<object> ConfigureProviderAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var agentType = data.Value.GetProperty("agentType").GetString();
            var providerName = data.Value.GetProperty("providerName").GetString();
            var modelOverride = data.Value.TryGetProperty("modelOverride", out var model) ? model.GetString() : null;

            _providerConfigService.SetProviderForAgent(agentType!, providerName!, modelOverride);
            return new { success = true, message = $"Updated {agentType} to use {providerName}" };
        }

        private async Task<object> ValidateProviderAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var providerName = data.Value.GetProperty("providerName").GetString();
            var (isValid, message) = _providerConfigService.ValidateProvider(providerName!);
            return new { isValid, message, providerName };
        }

        private async Task<object> GetProvidersForAgentAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var agentType = data.Value.GetProperty("agentType").GetString();
            var providers = _providerConfigService.GetProvidersForAgent(agentType!);
            var currentProvider = _providerConfigService.GetProviderForAgent(agentType!);

            return new
            {
                agentType,
                currentProvider,
                availableProviders = providers.Select(p => new
                {
                    p.Name,
                    p.DisplayName,
                    p.DefaultModel,
                    p.Description,
                    IsConfigured = _providerConfigService.ValidateProvider(p.Name).isValid
                })
            };
        }

        private async Task<object> GetProjectConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectService.GetProject(projectId!);
            if (project == null)
            {
                throw new InvalidOperationException("Project not found");
            }

            return new
            {
                projectId,
                projectName = project.Name,
                maxParallelKobolds = project.MaxParallelKobolds
            };
        }

        private async Task<object> UpdateProjectConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var maxParallelKobolds = data.Value.GetProperty("maxParallelKobolds").GetInt32();

            _projectService.SetMaxParallelKobolds(projectId!, maxParallelKobolds);
            return new { success = true, message = "Project configuration updated" };
        }

        private async Task<object> GetProjectProvidersAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectService.GetProject(projectId!);
            if (project == null)
            {
                throw new InvalidOperationException("Project not found");
            }

            var config = _projectService.GetProjectConfig(projectId!);

            return new
            {
                projectId,
                projectName = project.Name,
                providers = new
                {
                    WyvernProvider = config.WyvernProvider,
                    WyvernModel = config.WyvernModel,
                    WyvernEnabled = config.WyvernEnabled,
                    drakeProvider = config.DrakeProvider,
                    drakeModel = config.DrakeModel,
                    drakeEnabled = config.DrakeEnabled,
                    koboldProvider = config.KoboldProvider,
                    koboldModel = config.KoboldModel,
                    koboldEnabled = config.KoboldEnabled,
                    lastUpdated = config.LastUpdated
                },
                availableProviders = _providerConfigService.GetAvailableProviders().Select(p => new
                {
                    p.Name,
                    p.DisplayName,
                    p.DefaultModel,
                    p.CompatibleAgents,
                    IsConfigured = _providerConfigService.ValidateProvider(p.Name).isValid
                })
            };
        }

        private async Task<object> UpdateProjectProvidersAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();
            var providerName = data.Value.GetProperty("providerName").GetString();
            var modelOverride = data.Value.TryGetProperty("modelOverride", out var model) ? model.GetString() : null;

            _projectService.SetProjectProviders(projectId!, agentType!, providerName!, modelOverride);
            return new { success = true, message = "Provider settings updated for project" };
        }

        private async Task<object> ToggleAgentAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();
            var enabled = data.Value.GetProperty("enabled").GetBoolean();

            _projectService.SetAgentEnabled(projectId!, agentType!, enabled);
            var status = enabled ? "enabled" : "disabled";
            return new { success = true, message = $"{agentType} {status} for project", enabled };
        }

        private async Task<object> GetAgentStatusAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();

            var enabled = _projectService.IsAgentEnabled(projectId!, agentType!);
            return new { projectId, agentType, enabled };
        }

        private async Task<object> GetAllProjectConfigsAsync()
        {
            var configs = _projectConfigService.GetAllProjectConfigs();
            var limits = _providerConfigService.GetDefaultLimits();

            return new
            {
                defaults = new
                {
                    maxParallelKobolds = limits.MaxParallelKobolds,
                    maxParallelDrakes = limits.MaxParallelDrakes,
                    maxParallelWyrms = limits.MaxParallelWyrms,
                    maxParallelWyverns = limits.MaxParallelWyverns
                },
                projects = configs
            };
        }

        private async Task<object> GetProjectConfigFullAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var config = _projectConfigService.GetProjectConfig(projectId!);
            if (config == null)
            {
                throw new InvalidOperationException($"Configuration not found for project: {projectId}");
            }

            return config;
        }

        private async Task<object> UpdateProjectConfigFullAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var config = JsonSerializer.Deserialize<ProjectConfig>(data.Value.GetRawText(), options);
            if (config != null)
            {
                config.ProjectId = projectId!;
                _projectConfigService.UpdateProjectConfig(config);
            }

            return new { success = true, message = "Project configuration updated", config };
        }

        private async Task<object> DeleteProjectConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var deleted = _projectConfigService.DeleteProjectConfig(projectId!);
            if (!deleted)
            {
                throw new InvalidOperationException($"Configuration not found for project: {projectId}");
            }

            return new { success = true, message = "Project configuration deleted" };
        }

        private async Task<object> GetAgentConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();
            var config = _projectConfigService.GetProjectConfig(projectId!);
            if (config == null)
            {
                throw new InvalidOperationException($"Configuration not found for project: {projectId}");
            }

            var agentConfig = agentType!.ToLowerInvariant() switch
            {
                "wyvern" or "wyvern" => new
                {
                    provider = config.WyvernProvider,
                    model = config.WyvernModel,
                    enabled = config.WyvernEnabled
                },
                "drake" => new
                {
                    provider = config.DrakeProvider,
                    model = config.DrakeModel,
                    enabled = config.DrakeEnabled
                },
                "kobold" => new
                {
                    provider = config.KoboldProvider,
                    model = config.KoboldModel,
                    enabled = config.KoboldEnabled
                },
                _ => throw new InvalidOperationException($"Unknown agent type: {agentType}")
            };

            return agentConfig;
        }

        private async Task<object> UpdateAgentConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();
            var provider = data.Value.TryGetProperty("provider", out var p) ? p.GetString() : null;
            var model = data.Value.TryGetProperty("model", out var m) ? m.GetString() : null;
            var enabled = data.Value.TryGetProperty("enabled", out var e) ? e.GetBoolean() : (bool?)null;

            if (provider != null)
            {
                _projectConfigService.SetProjectProvider(projectId!, agentType!, provider, model);
            }

            if (enabled.HasValue)
            {
                _projectConfigService.SetAgentEnabled(projectId!, agentType!, enabled.Value);
            }

            return new { success = true, message = $"{agentType} configuration updated" };
        }

        private async Task<object> RetryAnalysisAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var success = _projectService.RetryAnalysis(projectId!);

            if (!success)
            {
                var project = _projectService.GetProject(projectId!);
                if (project == null)
                {
                    throw new InvalidOperationException($"Project not found: {projectId}");
                }
                throw new InvalidOperationException($"Cannot retry analysis - project status is {project.Status}, not Failed");
            }

            return new { success = true, message = "Analysis retry initiated. Project will be reprocessed shortly." };
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

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var json = JsonSerializer.Serialize(response, options);
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

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
            var json = JsonSerializer.Serialize(response, options);
            _logger.LogWarning("Sending error: {Error}", json);
            var bytes = Encoding.UTF8.GetBytes(json);
            await webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}
