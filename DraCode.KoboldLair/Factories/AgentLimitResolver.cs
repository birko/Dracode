using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Models.Configuration;

namespace DraCode.KoboldLair.Factories
{
    /// <summary>
    /// Resolves a project's parallel limit for an agent type: the project's stored limit when the
    /// repository knows the project, otherwise the global default from configuration.
    /// </summary>
    public static class AgentLimitResolver
    {
        public static int GetMaxParallel(IProjectRepository? projectRepository, AgentLimits defaults, string? projectId, string agentType)
        {
            var defaultValue = defaults.GetDefaultMaxParallel(agentType);
            if (projectRepository == null || string.IsNullOrEmpty(projectId))
                return defaultValue;

            return projectRepository.GetMaxParallel(projectId, agentType, defaultValue);
        }
    }
}
