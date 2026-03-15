using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;

namespace DraCode.KoboldLair.Data.Repositories
{
    /// <summary>
    /// Abstraction for project persistence. Supports both JSON file storage
    /// and SQL database backends via Birko.Data.
    /// </summary>
    public interface IProjectRepository
    {
        // CRUD Operations
        void Add(Project project);
        Task AddAsync(Project project);
        void Update(Project project);
        Task UpdateAsync(Project project);
        void Delete(string id);
        Task DeleteAsync(string id);

        // Query Operations
        Project? GetById(string id);
        Project? GetByName(string name);
        Project? GetBySpecificationPath(string specPath);
        List<Project> GetAll();
        List<Project> GetByStatus(ProjectStatus status);
        List<Project> GetByStatuses(params ProjectStatus[] statuses);
        int Count();

        // Agent Configuration
        AgentConfig? GetAgentConfig(string projectId, string agentType);
        int GetMaxParallel(string projectId, string agentType, int defaultValue = 1);
        Task SetAgentLimitAsync(string projectId, string agentType, int maxParallel);
        Task SetAgentTimeoutAsync(string projectId, string agentType, int timeoutSeconds);
        int GetAgentTimeout(string projectId, string agentType);
        Task SetAgentEnabledAsync(string projectId, string agentType, bool enabled);
        bool IsAgentEnabled(string projectId, string agentType);
        Task SetProjectProviderAsync(string projectId, string agentType, string? provider, string? model = null);
        string? GetProjectProvider(string projectId, string agentType);
        string? GetProjectModel(string projectId, string agentType);

        // Security / External Paths
        Task AddAllowedExternalPathAsync(string projectId, string path);
        Task<bool> RemoveAllowedExternalPathAsync(string projectId, string path);
        IReadOnlyList<string> GetAllowedExternalPaths(string projectId);
        Task SetSandboxModeAsync(string projectId, string mode);
        string GetSandboxMode(string projectId);
    }
}
