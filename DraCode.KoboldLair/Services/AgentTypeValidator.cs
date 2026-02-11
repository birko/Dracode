using DraCode.Agent.Agents;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Validates and normalizes agent types to ensure only valid agent types are used.
    /// Prevents area names (like "frontend", "backend") from being used as agent types.
    /// Valid types are loaded from AgentFactory at startup.
    /// </summary>
    public static class AgentTypeValidator
    {
        /// <summary>
        /// Valid agent types loaded from AgentFactory.
        /// Area names (frontend, backend, etc.) are NOT valid agent types.
        /// </summary>
        public static readonly HashSet<string> ValidAgentTypes;

        /// <summary>
        /// Maps common invalid agent types (area names) to valid agent types.
        /// Used when LLM mistakenly uses area names as agent types.
        /// </summary>
        private static readonly Dictionary<string, string> AreaToAgentMapping = new(StringComparer.OrdinalIgnoreCase)
        {
            // Area names -> default agent types
            { "frontend", "javascript" },  // Default frontend to JS
            { "backend", "coding" },       // Default backend to general coding
            { "database", "coding" },      // Database tasks use general coding
            { "infrastructure", "coding" },// Infrastructure uses general coding
            { "testing", "coding" },       // Testing area (note: "test" agent type is valid)
            { "security", "coding" },      // Security uses general coding
            { "web", "javascript" },       // Web defaults to JS
            { "api", "coding" },           // API tasks use general coding
            { "ui", "css" },               // UI defaults to CSS
            { "general", "coding" },       // General fallback
        };

        static AgentTypeValidator()
        {
            // Load valid agent types from AgentFactory (includes primary types)
            ValidAgentTypes = new HashSet<string>(
                AgentFactory.SupportedAgentTypes,
                StringComparer.OrdinalIgnoreCase);

            // Also include aliases as valid inputs (they'll be normalized to primary types)
            foreach (var alias in AgentFactory.AgentTypeAliases.Keys)
            {
                ValidAgentTypes.Add(alias);
            }
        }

        /// <summary>
        /// Validates and normalizes an agent type.
        /// Returns the valid agent type, or "coding" as fallback.
        /// </summary>
        /// <param name="agentType">The agent type to validate</param>
        /// <returns>A valid agent type string (normalized to lowercase)</returns>
        public static string Normalize(string? agentType)
        {
            if (string.IsNullOrWhiteSpace(agentType))
                return "coding";

            var lowerType = agentType.ToLowerInvariant();

            // Check if it's a valid primary agent type
            if (AgentFactory.SupportedAgentTypes.Contains(lowerType, StringComparer.OrdinalIgnoreCase))
                return lowerType;

            // Check if it's an alias and map to primary type
            if (AgentFactory.AgentTypeAliases.TryGetValue(agentType, out var primaryType))
                return primaryType;

            // Try to map area names to valid agent types
            if (AreaToAgentMapping.TryGetValue(agentType, out var mapped))
                return mapped;

            // Fallback to coding
            return "coding";
        }

        /// <summary>
        /// Checks if an agent type is valid (includes aliases).
        /// </summary>
        /// <param name="agentType">The agent type to check</param>
        /// <returns>True if the agent type is valid or is a known alias</returns>
        public static bool IsValid(string? agentType)
        {
            if (string.IsNullOrWhiteSpace(agentType))
                return false;

            return ValidAgentTypes.Contains(agentType);
        }

        /// <summary>
        /// Gets a comma-separated list of valid agent types for error messages.
        /// </summary>
        public static string GetValidTypesString()
        {
            return string.Join(", ", AgentFactory.SupportedAgentTypes.Order());
        }
    }
}
