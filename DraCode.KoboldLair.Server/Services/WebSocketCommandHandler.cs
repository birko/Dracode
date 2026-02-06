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
        // Cached JsonSerializerOptions to avoid reflection overhead on every message
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

        private static readonly JsonSerializerOptions s_readOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ILogger<WebSocketCommandHandler> _logger;
        private readonly ProjectService _projectService;
        private readonly DragonService _dragonService;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly ProjectRepository _projectRepository;
        private readonly DrakeFactory _drakeFactory;
        private readonly WyvernFactory _wyvernFactory;

        public WebSocketCommandHandler(
            ILogger<WebSocketCommandHandler> logger,
            ProjectService projectService,
            DragonService dragonService,
            ProviderConfigurationService providerConfigService,
            ProjectRepository projectRepository,
            DrakeFactory drakeFactory,
            WyvernFactory wyvernFactory)
        {
            _logger = logger;
            _projectService = projectService;
            _dragonService = dragonService;
            _providerConfigService = providerConfigService;
            _projectRepository = projectRepository;
            _drakeFactory = drakeFactory;
            _wyvernFactory = wyvernFactory;
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
                    "get_hierarchy" => await GetHierarchyAsync(),
                    "get_projects" => await GetProjectsAsync(),
                    "get_project_agents" => await GetProjectAgentsAsync(message.Data),
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
                    projects = projects.Where(p => p.Tracking.WyvernId != null).Select(p =>
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

        private async Task<object> GetProjectAgentsAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectService.GetProject(projectId!);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            // Get Wyvern info
            var wyvern = _wyvernFactory.GetWyvern(project.Name);
            var wyvernCount = wyvern != null ? 1 : 0;

            // Get Drake info for this project
            var drake = _drakeFactory.GetDrake(project.Name);
            var drakeCount = drake != null ? 1 : 0;
            var kobolds = 0;

            if (drake != null)
            {
                var stats = drake.GetStatistics();
                kobolds = stats.WorkingKobolds;
            }

            return new
            {
                projectId,
                projectName = project.Name,
                agents = new
                {
                    wyverns = wyvernCount,
                    drakes = drakeCount,
                    kobolds = kobolds
                }
            };
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
                maxParallelKobolds = project.Agents.Kobold.MaxParallel
            };
        }

        private async Task<object> UpdateProjectConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var maxParallelKobolds = data.Value.GetProperty("maxParallelKobolds").GetInt32();

            await _projectService.SetMaxParallelKoboldsAsync(projectId!, maxParallelKobolds);
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
                    WyvernProvider = project.Agents.Wyvern.Provider,
                    WyvernModel = project.Agents.Wyvern.Model,
                    WyvernEnabled = project.Agents.Wyvern.Enabled,
                    drakeProvider = project.Agents.Drake.Provider,
                    drakeModel = project.Agents.Drake.Model,
                    drakeEnabled = project.Agents.Drake.Enabled,
                    koboldProvider = project.Agents.Kobold.Provider,
                    koboldModel = project.Agents.Kobold.Model,
                    koboldEnabled = project.Agents.Kobold.Enabled,
                    lastUpdated = project.Timestamps.UpdatedAt
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

            await _projectService.SetProjectProvidersAsync(projectId!, agentType!, providerName!, modelOverride);
            return new { success = true, message = "Provider settings updated for project" };
        }

        private async Task<object> ToggleAgentAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();
            var enabled = data.Value.GetProperty("enabled").GetBoolean();

            await _projectService.SetAgentEnabledAsync(projectId!, agentType!, enabled);
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
            var projects = _projectRepository.GetAll();
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
                projects = projects.Select(p => new
                {
                    project = new { p.Id, p.Name },
                    agents = p.Agents,
                    security = p.Security,
                    metadata = new { lastUpdated = p.Timestamps.UpdatedAt, createdAt = p.Timestamps.CreatedAt }
                })
            };
        }

        private async Task<object> GetProjectConfigFullAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectRepository.GetById(projectId!);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            return new
            {
                project = new { project.Id, project.Name },
                agents = project.Agents,
                security = project.Security,
                metadata = new { lastUpdated = project.Timestamps.UpdatedAt, createdAt = project.Timestamps.CreatedAt }
            };
        }

        private async Task<object> UpdateProjectConfigFullAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectRepository.GetById(projectId!);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            // Update agent configurations if provided
            if (data.Value.TryGetProperty("agents", out var agentsElement))
            {
                var agents = JsonSerializer.Deserialize<AgentsConfig>(agentsElement.GetRawText(), s_readOptions);
                if (agents != null)
                {
                    project.Agents = agents;
                }
            }

            // Update security settings if provided
            if (data.Value.TryGetProperty("security", out var securityElement))
            {
                var security = JsonSerializer.Deserialize<SecurityConfig>(securityElement.GetRawText(), s_readOptions);
                if (security != null)
                {
                    project.Security = security;
                }
            }

            await _projectRepository.UpdateAsync(project);

            return new { success = true, message = "Project configuration updated", config = new
            {
                project = new { project.Id, project.Name },
                agents = project.Agents,
                security = project.Security,
                metadata = new { lastUpdated = project.Timestamps.UpdatedAt, createdAt = project.Timestamps.CreatedAt }
            }};
        }

        private async Task<object> DeleteProjectConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            
            // Note: We don't delete the project, just reset its configuration to defaults
            var project = _projectRepository.GetById(projectId!);
            if (project == null)
            {
                throw new InvalidOperationException($"Project not found: {projectId}");
            }

            // Reset to default configuration
            project.Agents = new AgentsConfig
            {
                Wyrm = new AgentConfig { Enabled = true, MaxParallel = 1 },
                Wyvern = new AgentConfig { Enabled = true, MaxParallel = 1 },
                Drake = new AgentConfig { Enabled = true, MaxParallel = 1 },
                KoboldPlanner = new AgentConfig { Enabled = true, MaxParallel = 1 },
                Kobold = new AgentConfig { Enabled = true, MaxParallel = 4 }
            };
            project.Security = new SecurityConfig();

            await _projectRepository.UpdateAsync(project);

            return new { success = true, message = "Project configuration reset to defaults" };
        }

        private async Task<object> GetAgentConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();
            
            var agentConfig = _projectRepository.GetAgentConfig(projectId!, agentType!);
            if (agentConfig == null)
            {
                throw new InvalidOperationException($"Configuration not found for agent {agentType} in project: {projectId}");
            }

            return new
            {
                provider = agentConfig.Provider,
                model = agentConfig.Model,
                enabled = agentConfig.Enabled,
                maxParallel = agentConfig.MaxParallel,
                timeout = agentConfig.Timeout
            };
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
                await _projectRepository.SetProjectProviderAsync(projectId!, agentType!, provider, model);
            }

            if (enabled.HasValue)
            {
                await _projectRepository.SetAgentEnabledAsync(projectId!, agentType!, enabled.Value);
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
