using DraCode.Agent.Tools;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Configuration data for a project's agents
    /// </summary>
    public class ProjectAgentConfig
    {
        public string ProjectId { get; set; } = string.Empty;
        public string? ProjectName { get; set; }

        // Enabled states
        public bool WyvernEnabled { get; set; }
        public bool WyrmEnabled { get; set; }
        public bool DrakeEnabled { get; set; }
        public bool KoboldEnabled { get; set; }

        // Parallel limits
        public int MaxParallelWyverns { get; set; }
        public int MaxParallelWyrms { get; set; }
        public int MaxParallelDrakes { get; set; }
        public int MaxParallelKobolds { get; set; }

        // Providers
        public string? WyvernProvider { get; set; }
        public string? WyrmProvider { get; set; }
        public string? DrakeProvider { get; set; }
        public string? KoboldProvider { get; set; }
    }

    /// <summary>
    /// Tool for Dragon to view and manage agent configurations for projects.
    /// Allows checking if agents (Wyvern, Wyrm, Drake, Kobold) are enabled,
    /// viewing/changing parallel limits, and toggling agent states.
    /// </summary>
    public class AgentConfigurationTool : Tool
    {
        private readonly Func<string, ProjectAgentConfig?>? _getProjectConfig;
        private readonly Func<List<(string Id, string Name)>>? _getAllProjects;
        private readonly Action<string, string, bool>? _setAgentEnabled;
        private readonly Action<string, string, int>? _setAgentLimit;

        /// <summary>
        /// Creates a new AgentConfigurationTool
        /// </summary>
        /// <param name="getProjectConfig">Function to get project agent configuration by project ID or name</param>
        /// <param name="getAllProjects">Function to get list of all projects (Id, Name)</param>
        /// <param name="setAgentEnabled">Action to set agent enabled state (projectId, agentType, enabled)</param>
        /// <param name="setAgentLimit">Action to set agent parallel limit (projectId, agentType, limit)</param>
        public AgentConfigurationTool(
            Func<string, ProjectAgentConfig?>? getProjectConfig,
            Func<List<(string Id, string Name)>>? getAllProjects,
            Action<string, string, bool>? setAgentEnabled,
            Action<string, string, int>? setAgentLimit)
        {
            _getProjectConfig = getProjectConfig;
            _getAllProjects = getAllProjects;
            _setAgentEnabled = setAgentEnabled;
            _setAgentLimit = setAgentLimit;
        }

        public override string Name => "manage_agents";

        public override string Description =>
            "View and manage agent configurations for projects. " +
            "Check if Wyvern, Wyrm, Drake, and Kobold agents are enabled or disabled, " +
            "view parallel limits, and change these settings per project. " +
            "Actions: 'status' (view all projects), 'get' (view one project), 'enable', 'disable', 'set_limit'.";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action to perform: 'status' (all projects summary), 'get' (single project details), 'enable' (enable agent), 'disable' (disable agent), 'set_limit' (change parallel limit)",
                    @enum = new[] { "status", "get", "enable", "disable", "set_limit" }
                },
                project = new
                {
                    type = "string",
                    description = "Project name or ID (required for get, enable, disable, set_limit)"
                },
                agent_type = new
                {
                    type = "string",
                    description = "Agent type: 'wyvern', 'wyrm', 'drake', 'kobold' (required for enable, disable, set_limit)",
                    @enum = new[] { "wyvern", "wyrm", "drake", "kobold" }
                },
                limit = new
                {
                    type = "integer",
                    description = "New parallel limit value (required for set_limit, must be >= 1)",
                    minimum = 1
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var actionObj) ? actionObj?.ToString()?.ToLowerInvariant() : null;
            var project = input.TryGetValue("project", out var projObj) ? projObj?.ToString() : null;
            var agentType = input.TryGetValue("agent_type", out var agentObj) ? agentObj?.ToString()?.ToLowerInvariant() : null;
            var limit = input.TryGetValue("limit", out var limitObj) ? Convert.ToInt32(limitObj) : 0;

            return action switch
            {
                "status" => GetAllProjectsStatus(),
                "get" => GetProjectDetails(project),
                "enable" => SetAgentEnabled(project, agentType, true),
                "disable" => SetAgentEnabled(project, agentType, false),
                "set_limit" => SetAgentLimit(project, agentType, limit),
                _ => "Unknown action. Use 'status', 'get', 'enable', 'disable', or 'set_limit'."
            };
        }

        private string GetAllProjectsStatus()
        {
            if (_getAllProjects == null || _getProjectConfig == null)
            {
                return "Agent configuration service not available.";
            }

            try
            {
                var projects = _getAllProjects();
                if (projects.Count == 0)
                {
                    return "No projects found. Create a project first to configure agents.";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"## Agent Status for {projects.Count} Project(s)\n");

                foreach (var (id, name) in projects)
                {
                    var config = _getProjectConfig(id);
                    if (config == null)
                    {
                        result.AppendLine($"### {name}");
                        result.AppendLine("   ⚠️ No configuration found\n");
                        continue;
                    }

                    result.AppendLine($"### {name}");
                    result.AppendLine($"| Agent   | Enabled | Max Parallel | Provider |");
                    result.AppendLine($"|---------|---------|--------------|----------|");
                    result.AppendLine($"| Wyvern  | {(config.WyvernEnabled ? "✅ Yes" : "❌ No")} | {config.MaxParallelWyverns} | {config.WyvernProvider ?? "default"} |");
                    result.AppendLine($"| Wyrm    | {(config.WyrmEnabled ? "✅ Yes" : "❌ No")} | {config.MaxParallelWyrms} | {config.WyrmProvider ?? "default"} |");
                    result.AppendLine($"| Drake   | {(config.DrakeEnabled ? "✅ Yes" : "❌ No")} | {config.MaxParallelDrakes} | {config.DrakeProvider ?? "default"} |");
                    result.AppendLine($"| Kobold  | {(config.KoboldEnabled ? "✅ Yes" : "❌ No")} | {config.MaxParallelKobolds} | {config.KoboldProvider ?? "default"} |");
                    result.AppendLine();
                }

                result.AppendLine("**Legend:**");
                result.AppendLine("- **Wyvern**: Analyzes specifications and creates task breakdowns");
                result.AppendLine("- **Wyrm**: Task analysis (if separate from Wyvern)");
                result.AppendLine("- **Drake**: Supervises task execution and manages Kobolds");
                result.AppendLine("- **Kobold**: Code generation workers");

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting project status: {ex.Message}";
            }
        }

        private string GetProjectDetails(string? project)
        {
            if (string.IsNullOrEmpty(project))
            {
                return "Error: 'project' parameter is required for 'get' action.";
            }

            if (_getProjectConfig == null)
            {
                return "Agent configuration service not available.";
            }

            try
            {
                var config = _getProjectConfig(project);
                if (config == null)
                {
                    return $"No configuration found for project: {project}. The project may not exist or has no configuration yet.";
                }

                var result = new System.Text.StringBuilder();
                result.AppendLine($"## Agent Configuration: {config.ProjectName ?? config.ProjectId}\n");

                result.AppendLine("### Enabled Status");
                result.AppendLine($"| Agent   | Status |");
                result.AppendLine($"|---------|--------|");
                result.AppendLine($"| Wyvern  | {(config.WyvernEnabled ? "✅ Enabled" : "❌ Disabled")} |");
                result.AppendLine($"| Wyrm    | {(config.WyrmEnabled ? "✅ Enabled" : "❌ Disabled")} |");
                result.AppendLine($"| Drake   | {(config.DrakeEnabled ? "✅ Enabled" : "❌ Disabled")} |");
                result.AppendLine($"| Kobold  | {(config.KoboldEnabled ? "✅ Enabled" : "❌ Disabled")} |");
                result.AppendLine();

                result.AppendLine("### Parallel Limits");
                result.AppendLine($"| Agent   | Max Parallel |");
                result.AppendLine($"|---------|--------------|");
                result.AppendLine($"| Wyvern  | {config.MaxParallelWyverns} |");
                result.AppendLine($"| Wyrm    | {config.MaxParallelWyrms} |");
                result.AppendLine($"| Drake   | {config.MaxParallelDrakes} |");
                result.AppendLine($"| Kobold  | {config.MaxParallelKobolds} |");
                result.AppendLine();

                result.AppendLine("### Provider Overrides");
                result.AppendLine($"| Agent   | Provider |");
                result.AppendLine($"|---------|----------|");
                result.AppendLine($"| Wyvern  | {config.WyvernProvider ?? "(using default)"} |");
                result.AppendLine($"| Wyrm    | {config.WyrmProvider ?? "(using default)"} |");
                result.AppendLine($"| Drake   | {config.DrakeProvider ?? "(using default)"} |");
                result.AppendLine($"| Kobold  | {config.KoboldProvider ?? "(using default)"} |");

                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting project details: {ex.Message}";
            }
        }

        private string SetAgentEnabled(string? project, string? agentType, bool enabled)
        {
            if (string.IsNullOrEmpty(project))
            {
                return "Error: 'project' parameter is required.";
            }
            if (string.IsNullOrEmpty(agentType))
            {
                return "Error: 'agent_type' parameter is required (wyvern, wyrm, drake, or kobold).";
            }
            if (!IsValidAgentType(agentType))
            {
                return $"Error: Invalid agent type '{agentType}'. Must be one of: wyvern, wyrm, drake, kobold.";
            }
            if (_setAgentEnabled == null || _getProjectConfig == null)
            {
                return "Agent configuration service not available.";
            }

            try
            {
                // Verify project exists
                var config = _getProjectConfig(project);
                if (config == null)
                {
                    return $"Error: Project '{project}' not found.";
                }

                _setAgentEnabled(config.ProjectId, agentType, enabled);

                var agentName = char.ToUpper(agentType[0]) + agentType[1..];
                var action = enabled ? "enabled" : "disabled";
                return $"✅ {agentName} has been {action} for project '{config.ProjectName ?? config.ProjectId}'.";
            }
            catch (Exception ex)
            {
                return $"Error setting agent state: {ex.Message}";
            }
        }

        private string SetAgentLimit(string? project, string? agentType, int limit)
        {
            if (string.IsNullOrEmpty(project))
            {
                return "Error: 'project' parameter is required.";
            }
            if (string.IsNullOrEmpty(agentType))
            {
                return "Error: 'agent_type' parameter is required (wyvern, wyrm, drake, or kobold).";
            }
            if (!IsValidAgentType(agentType))
            {
                return $"Error: Invalid agent type '{agentType}'. Must be one of: wyvern, wyrm, drake, kobold.";
            }
            if (limit < 1)
            {
                return "Error: 'limit' must be at least 1.";
            }
            if (_setAgentLimit == null || _getProjectConfig == null)
            {
                return "Agent configuration service not available.";
            }

            try
            {
                // Verify project exists
                var config = _getProjectConfig(project);
                if (config == null)
                {
                    return $"Error: Project '{project}' not found.";
                }

                _setAgentLimit(config.ProjectId, agentType, limit);

                var agentName = char.ToUpper(agentType[0]) + agentType[1..];
                return $"✅ {agentName} max parallel limit set to {limit} for project '{config.ProjectName ?? config.ProjectId}'.";
            }
            catch (Exception ex)
            {
                return $"Error setting agent limit: {ex.Message}";
            }
        }

        private static bool IsValidAgentType(string agentType)
        {
            return agentType == "wyvern" || agentType == "wyrm" || agentType == "drake" || agentType == "kobold";
        }
    }
}
