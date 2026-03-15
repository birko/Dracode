using Birko.Data.SQL.Repositories;
using Birko.Data.Stores;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Models.Tasks;
using Microsoft.Extensions.Logging;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQL-backed task repository using Birko.Data with SQLite.
    /// Replaces per-area JSON file storage with transactional database writes.
    /// </summary>
    public class SqlTaskRepository : ITaskRepository
    {
        private readonly AsyncSqLiteModelRepository<TaskEntity> _repository;
        private readonly ILogger<SqlTaskRepository>? _logger;

        public SqlTaskRepository(string dbPath, ILogger<SqlTaskRepository>? logger = null)
        {
            _logger = logger;
            _repository = new AsyncSqLiteModelRepository<TaskEntity>();

            var dbDir = Path.GetDirectoryName(dbPath) ?? ".";
            var dbFile = Path.GetFileName(dbPath);
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            _repository.SetSettings(new PasswordSettings(dbDir, dbFile));
        }

        public async Task InitializeAsync()
        {
            await _repository.InitAsync();
            _logger?.LogInformation("SQLite task repository initialized");
        }

        #region CRUD Operations

        public async Task<string> AddTaskAsync(string projectId, string areaName, TaskRecord task)
        {
            task.ProjectId = projectId;
            var entity = EntityMapper.ToEntity(task, areaName);
            await _repository.CreateAsync(entity);
            _logger?.LogDebug("Added task {TaskId} to project {ProjectId} area {Area}", task.Id, projectId, areaName);
            return task.Id;
        }

        public async Task UpdateTaskAsync(TaskRecord task)
        {
            // Single-entity read: pass CancellationToken to select the T? overload
            var existing = await _repository.ReadAsync(e => e.TaskId == task.Id, CancellationToken.None);
            if (existing == null)
                throw new InvalidOperationException($"Task not found: {task.Id}");

            EntityMapper.UpdateEntity(existing, task);
            await _repository.UpdateAsync(existing);
        }

        public async Task DeleteTaskAsync(string taskId)
        {
            var existing = await _repository.ReadAsync(e => e.TaskId == taskId, CancellationToken.None);
            if (existing != null)
            {
                await _repository.DeleteAsync(existing);
                _logger?.LogDebug("Deleted task {TaskId}", taskId);
            }
        }

        #endregion

        #region Query Operations

        public async Task<TaskRecord?> GetByIdAsync(string taskId)
        {
            var entity = await _repository.ReadAsync(e => e.TaskId == taskId, CancellationToken.None);
            return entity != null ? EntityMapper.ToTaskRecord(entity) : null;
        }

        public async Task<List<TaskRecord>> GetByProjectAsync(string projectId)
        {
            // Bulk read: pass all params to select the IEnumerable<T> overload
            var entities = await _repository.ReadAsync(
                filter: e => e.ProjectId == projectId, orderBy: null, limit: null, offset: null);
            return entities.Select(EntityMapper.ToTaskRecord).ToList();
        }

        public async Task<List<TaskRecord>> GetByProjectAndAreaAsync(string projectId, string areaName)
        {
            var entities = await _repository.ReadAsync(
                filter: e => e.ProjectId == projectId && e.AreaName == areaName,
                orderBy: null, limit: null, offset: null);
            return entities.Select(EntityMapper.ToTaskRecord).ToList();
        }

        public async Task<List<TaskRecord>> GetByStatusAsync(TaskStatus status)
        {
            var statusInt = (int)status;
            var entities = await _repository.ReadAsync(
                filter: e => e.Status == statusInt, orderBy: null, limit: null, offset: null);
            return entities.Select(EntityMapper.ToTaskRecord).ToList();
        }

        public async Task<List<TaskRecord>> GetByProjectAndStatusAsync(string projectId, TaskStatus status)
        {
            var statusInt = (int)status;
            var entities = await _repository.ReadAsync(
                filter: e => e.ProjectId == projectId && e.Status == statusInt,
                orderBy: null, limit: null, offset: null);
            return entities.Select(EntityMapper.ToTaskRecord).ToList();
        }

        public async Task<List<TaskRecord>> GetByPriorityAsync(TaskPriority priority)
        {
            var priorityInt = (int)priority;
            var entities = await _repository.ReadAsync(
                filter: e => e.Priority == priorityInt, orderBy: null, limit: null, offset: null);
            return entities.Select(EntityMapper.ToTaskRecord).ToList();
        }

        public async Task<int> CountByProjectAsync(string projectId)
        {
            var count = await _repository.CountAsync(e => e.ProjectId == projectId);
            return (int)count;
        }

        #endregion

        #region Bulk Operations

        public async Task UpdateTasksAsync(IEnumerable<TaskRecord> tasks)
        {
            foreach (var task in tasks)
            {
                await UpdateTaskAsync(task);
            }
        }

        #endregion

        #region Task-specific Operations

        public async Task SetErrorAsync(string taskId, string? errorMessage, string? errorCategory = null)
        {
            var existing = await _repository.ReadAsync(e => e.TaskId == taskId, CancellationToken.None);
            if (existing != null)
            {
                existing.ErrorMessage = errorMessage;
                existing.ErrorCategory = errorCategory;
                existing.Status = (int)TaskStatus.Failed;
                existing.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(existing);
            }
        }

        public async Task ClearErrorAsync(string taskId)
        {
            var existing = await _repository.ReadAsync(e => e.TaskId == taskId, CancellationToken.None);
            if (existing != null)
            {
                existing.ErrorMessage = null;
                existing.ErrorCategory = null;
                existing.NextRetryAt = null;
                existing.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(existing);
            }
        }

        public async Task SetStatusAsync(string taskId, TaskStatus status)
        {
            var existing = await _repository.ReadAsync(e => e.TaskId == taskId, CancellationToken.None);
            if (existing != null)
            {
                existing.Status = (int)status;
                existing.TaskUpdatedAt = DateTime.UtcNow;
                existing.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(existing);
            }
        }

        #endregion
    }
}
