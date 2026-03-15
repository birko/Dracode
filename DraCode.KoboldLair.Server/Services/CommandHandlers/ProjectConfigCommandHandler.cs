using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Services;
using System.Text.Json;

namespace DraCode.KoboldLair.Server.Services.CommandHandlers
{
    public class ProjectConfigCommandHandler
    {
        private static readonly JsonSerializerOptions s_readOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ProjectService _projectService;
        private readonly ProviderConfigurationService _providerConfigService;
        private readonly IProjectRepository _projectRepository;

        public ProjectConfigCommandHandler(
            ProjectService projectService,
            ProviderConfigurationService providerConfigService,
            IProjectRepository projectRepository)
        {
            _projectService = projectService;
            _providerConfigService = providerConfigService;
            _projectRepository = projectRepository;
        }

        public Task<object> GetProjectConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectService.GetProject(projectId!)
                ?? throw new InvalidOperationException("Project not found");

            return Task.FromResult<object>(new
            {
                projectId,
                projectName = project.Name,
                maxParallelKobolds = project.Agents.Kobold.MaxParallel
            });
        }

        public async Task<object> UpdateProjectConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var maxParallelKobolds = data.Value.GetProperty("maxParallelKobolds").GetInt32();

            await _projectService.SetMaxParallelKoboldsAsync(projectId!, maxParallelKobolds);
            return new { success = true, message = "Project configuration updated" };
        }

        public Task<object> GetProjectProvidersAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectService.GetProject(projectId!)
                ?? throw new InvalidOperationException("Project not found");

            return Task.FromResult<object>(new
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
            });
        }

        public async Task<object> UpdateProjectProvidersAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();
            var providerName = data.Value.GetProperty("providerName").GetString();
            var modelOverride = data.Value.TryGetProperty("modelOverride", out var model) ? model.GetString() : null;

            await _projectService.SetProjectProvidersAsync(projectId!, agentType!, providerName!, modelOverride);
            return new { success = true, message = "Provider settings updated for project" };
        }

        public async Task<object> ToggleAgentAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();
            var enabled = data.Value.GetProperty("enabled").GetBoolean();

            await _projectService.SetAgentEnabledAsync(projectId!, agentType!, enabled);
            var status = enabled ? "enabled" : "disabled";
            return new { success = true, message = $"{agentType} {status} for project", enabled };
        }

        public Task<object> GetAgentStatusAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();

            var enabled = _projectService.IsAgentEnabled(projectId!, agentType!);
            return Task.FromResult<object>(new { projectId, agentType, enabled });
        }

        public Task<object> GetAllProjectConfigsAsync()
        {
            var projects = _projectRepository.GetAll();
            var limits = _providerConfigService.GetDefaultLimits();

            return Task.FromResult<object>(new
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
            });
        }

        public Task<object> GetProjectConfigFullAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectRepository.GetById(projectId!)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            return Task.FromResult<object>(new
            {
                project = new { project.Id, project.Name },
                agents = project.Agents,
                security = project.Security,
                metadata = new { lastUpdated = project.Timestamps.UpdatedAt, createdAt = project.Timestamps.CreatedAt }
            });
        }

        public async Task<object> UpdateProjectConfigFullAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectRepository.GetById(projectId!)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

            if (data.Value.TryGetProperty("agents", out var agentsElement))
            {
                var agents = JsonSerializer.Deserialize<AgentsConfig>(agentsElement.GetRawText(), s_readOptions);
                if (agents != null)
                    project.Agents = agents;
            }

            if (data.Value.TryGetProperty("security", out var securityElement))
            {
                var security = JsonSerializer.Deserialize<SecurityConfig>(securityElement.GetRawText(), s_readOptions);
                if (security != null)
                    project.Security = security;
            }

            await _projectRepository.UpdateAsync(project);

            return new
            {
                success = true,
                message = "Project configuration updated",
                config = new
                {
                    project = new { project.Id, project.Name },
                    agents = project.Agents,
                    security = project.Security,
                    metadata = new { lastUpdated = project.Timestamps.UpdatedAt, createdAt = project.Timestamps.CreatedAt }
                }
            };
        }

        public async Task<object> DeleteProjectConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var project = _projectRepository.GetById(projectId!)
                ?? throw new InvalidOperationException($"Project not found: {projectId}");

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

        public Task<object> GetAgentConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();

            var agentConfig = _projectRepository.GetAgentConfig(projectId!, agentType!)
                ?? throw new InvalidOperationException($"Configuration not found for agent {agentType} in project: {projectId}");

            return Task.FromResult<object>(new
            {
                provider = agentConfig.Provider,
                model = agentConfig.Model,
                enabled = agentConfig.Enabled,
                maxParallel = agentConfig.MaxParallel,
                timeout = agentConfig.Timeout
            });
        }

        public async Task<object> UpdateAgentConfigAsync(JsonElement? data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            var projectId = data.Value.GetProperty("projectId").GetString();
            var agentType = data.Value.GetProperty("agentType").GetString();
            var provider = data.Value.TryGetProperty("provider", out var p) ? p.GetString() : null;
            var model = data.Value.TryGetProperty("model", out var m) ? m.GetString() : null;
            var enabled = data.Value.TryGetProperty("enabled", out var e) ? e.GetBoolean() : (bool?)null;

            if (provider != null)
                await _projectRepository.SetProjectProviderAsync(projectId!, agentType!, provider, model);

            if (enabled.HasValue)
                await _projectRepository.SetAgentEnabledAsync(projectId!, agentType!, enabled.Value);

            return new { success = true, message = $"{agentType} configuration updated" };
        }
    }
}
