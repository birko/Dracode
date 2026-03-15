using DraCode.Agent.Tools;
using DraCode.KoboldLair.Models.Configuration;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Tool for viewing and changing global user settings (provider preferences).
    /// Controls which LLM providers are used for Dragon, Wyrm, Wyvern, and Kobold agents globally.
    /// Also supports per-agent-type Kobold provider settings (e.g., use Claude for C#, OpenAI for Python).
    /// </summary>
    public class UserSettingsTool : Tool
    {
        private readonly Func<UserSettings>? _getUserSettings;
        private readonly Action<string, string, string?>? _setProviderForAgent;
        private readonly Action<string, string?, string?>? _setProviderForKoboldAgentType;
        private readonly Func<List<string>>? _getAvailableProviders;

        public UserSettingsTool(
            Func<UserSettings>? getUserSettings,
            Action<string, string, string?>? setProviderForAgent,
            Action<string, string?, string?>? setProviderForKoboldAgentType,
            Func<List<string>>? getAvailableProviders)
        {
            _getUserSettings = getUserSettings;
            _setProviderForAgent = setProviderForAgent;
            _setProviderForKoboldAgentType = setProviderForKoboldAgentType;
            _getAvailableProviders = getAvailableProviders;
        }

        public override string Name => "user_settings";

        public override string Description =>
            "View and change global LLM provider settings. " +
            "Controls which providers (openai, claude, gemini, zai, etc.) are used for each agent type. " +
            "Actions: 'view' (show current settings + available providers), " +
            "'set_provider' (set provider for dragon/wyrm/wyvern/kobold), " +
            "'set_kobold_type' (set provider for specific Kobold agent type like csharp, python, react).";

        public override object? InputSchema => new
        {
            type = "object",
            properties = new
            {
                action = new
                {
                    type = "string",
                    description = "Action: 'view' (show settings), 'set_provider' (change agent provider), 'set_kobold_type' (change per-type Kobold provider)",
                    @enum = new[] { "view", "set_provider", "set_kobold_type" }
                },
                agent_type = new
                {
                    type = "string",
                    description = "Agent type for set_provider: 'dragon', 'wyrm', 'wyvern', 'kobold'. For set_kobold_type: specific type like 'csharp', 'python', 'react', 'typescript', etc."
                },
                provider = new
                {
                    type = "string",
                    description = "Provider name (e.g., 'openai', 'claude', 'gemini', 'zai', 'ollama'). Use 'default' to clear override."
                },
                model = new
                {
                    type = "string",
                    description = "Optional model override (e.g., 'gpt-4o', 'claude-sonnet-4-20250514'). Omit to use provider default."
                }
            },
            required = new[] { "action" }
        };

        public override string Execute(string workingDirectory, Dictionary<string, object> input)
        {
            var action = input.TryGetValue("action", out var actionObj) ? actionObj?.ToString()?.ToLowerInvariant() : null;
            var agentType = input.TryGetValue("agent_type", out var atObj) ? atObj?.ToString()?.ToLowerInvariant() : null;
            var provider = input.TryGetValue("provider", out var provObj) ? provObj?.ToString()?.ToLowerInvariant() : null;
            var model = input.TryGetValue("model", out var modelObj) ? modelObj?.ToString() : null;

            return action switch
            {
                "view" => ViewSettings(),
                "set_provider" => SetProvider(agentType, provider, model),
                "set_kobold_type" => SetKoboldTypeProvider(agentType, provider, model),
                _ => "Unknown action. Use 'view', 'set_provider', or 'set_kobold_type'."
            };
        }

        private string ViewSettings()
        {
            if (_getUserSettings == null)
                return "User settings service not available.";

            try
            {
                var settings = _getUserSettings();
                var sb = new System.Text.StringBuilder();

                sb.AppendLine("## Global Provider Settings\n");
                sb.AppendLine("These settings control which LLM provider is used for each agent type across ALL projects.");
                sb.AppendLine("Per-project overrides (via `manage_agents`) take precedence over these.\n");

                sb.AppendLine("| Agent | Provider | Model |");
                sb.AppendLine("|-------|----------|-------|");
                sb.AppendLine($"| Dragon | {settings.DragonProvider ?? "(default)"} | {settings.DragonModel ?? "(provider default)"} |");
                sb.AppendLine($"| Wyrm | {settings.WyrmProvider ?? "(default)"} | {settings.WyrmModel ?? "(provider default)"} |");
                sb.AppendLine($"| Wyvern | {settings.WyvernProvider ?? "(default)"} | {settings.WyvernModel ?? "(provider default)"} |");
                sb.AppendLine($"| Kobold | {settings.KoboldProvider ?? "(default)"} | {settings.KoboldModel ?? "(provider default)"} |");
                sb.AppendLine();

                if (settings.KoboldAgentTypeSettings.Count > 0)
                {
                    sb.AppendLine("### Per-Type Kobold Overrides\n");
                    sb.AppendLine("| Agent Type | Provider | Model |");
                    sb.AppendLine("|------------|----------|-------|");
                    foreach (var kats in settings.KoboldAgentTypeSettings)
                    {
                        sb.AppendLine($"| {kats.AgentType} | {kats.Provider ?? "(default)"} | {kats.Model ?? "(provider default)"} |");
                    }
                    sb.AppendLine();
                }

                // Show available providers
                if (_getAvailableProviders != null)
                {
                    var providers = _getAvailableProviders();
                    if (providers.Count > 0)
                    {
                        sb.AppendLine($"### Available Providers\n");
                        sb.AppendLine(string.Join(", ", providers.Select(p => $"`{p}`")));
                        sb.AppendLine();
                    }
                }

                sb.AppendLine("**Resolution order**: Per-project override > Per-Kobold-type setting > Global agent setting > System default");

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Error reading settings: {ex.Message}";
            }
        }

        private string SetProvider(string? agentType, string? provider, string? model)
        {
            if (string.IsNullOrEmpty(agentType))
                return "Error: 'agent_type' is required (dragon, wyrm, wyvern, kobold).";

            if (string.IsNullOrEmpty(provider))
                return "Error: 'provider' is required. Use a provider name or 'default' to clear.";

            var validTypes = new[] { "dragon", "wyrm", "wyvern", "kobold" };
            if (!validTypes.Contains(agentType))
                return $"Error: Invalid agent type '{agentType}'. Must be one of: {string.Join(", ", validTypes)}.";

            if (_setProviderForAgent == null)
                return "User settings service not available.";

            try
            {
                var actualProvider = provider == "default" ? "" : provider;
                var actualModel = provider == "default" ? null : model;

                _setProviderForAgent(agentType, actualProvider, actualModel);

                var displayName = char.ToUpper(agentType[0]) + agentType[1..];
                if (provider == "default")
                    return $"Cleared provider override for {displayName}. Will use system default.";

                var modelMsg = !string.IsNullOrEmpty(model) ? $" with model '{model}'" : "";
                return $"Set {displayName} provider to '{provider}'{modelMsg}. Change takes effect on next agent creation.";
            }
            catch (Exception ex)
            {
                return $"Error setting provider: {ex.Message}";
            }
        }

        private string SetKoboldTypeProvider(string? agentType, string? provider, string? model)
        {
            if (string.IsNullOrEmpty(agentType))
                return "Error: 'agent_type' is required (e.g., 'csharp', 'python', 'react', 'typescript').";

            if (_setProviderForKoboldAgentType == null)
                return "User settings service not available.";

            try
            {
                var actualProvider = provider == "default" ? null : provider;
                var actualModel = provider == "default" ? null : model;

                _setProviderForKoboldAgentType(agentType, actualProvider, actualModel);

                if (provider == null || provider == "default")
                    return $"Cleared Kobold provider override for '{agentType}'. Will use global Kobold setting.";

                var modelMsg = !string.IsNullOrEmpty(model) ? $" with model '{model}'" : "";
                return $"Set Kobold '{agentType}' provider to '{provider}'{modelMsg}. New Kobolds of this type will use this provider.";
            }
            catch (Exception ex)
            {
                return $"Error setting Kobold type provider: {ex.Message}";
            }
        }
    }
}
