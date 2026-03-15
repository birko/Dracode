using DraCode.KoboldLair.Models.Tasks;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Data.Repositories
{
    /// <summary>
    /// Abstraction for task persistence. Supports both JSON file storage
    /// and SQL database backends via Birko.Data.
    /// </summary>
    public interface ITaskRepository
    {
        // CRUD Operations
        Task<string> AddTaskAsync(string projectId, string areaName, TaskRecord task);
        Task UpdateTaskAsync(TaskRecord task);
        Task DeleteTaskAsync(string taskId);

        // Query Operations
        Task<TaskRecord?> GetByIdAsync(string taskId);
        Task<List<TaskRecord>> GetByProjectAsync(string projectId);
        Task<List<TaskRecord>> GetByProjectAndAreaAsync(string projectId, string areaName);
        Task<List<TaskRecord>> GetByStatusAsync(TaskStatus status);
        Task<List<TaskRecord>> GetByProjectAndStatusAsync(string projectId, TaskStatus status);
        Task<List<TaskRecord>> GetByPriorityAsync(TaskPriority priority);
        Task<int> CountByProjectAsync(string projectId);

        // Bulk Operations
        Task UpdateTasksAsync(IEnumerable<TaskRecord> tasks);

        // Task-specific Operations
        Task SetErrorAsync(string taskId, string? errorMessage, string? errorCategory = null);
        Task ClearErrorAsync(string taskId);
        Task SetStatusAsync(string taskId, TaskStatus status);
    }
}
