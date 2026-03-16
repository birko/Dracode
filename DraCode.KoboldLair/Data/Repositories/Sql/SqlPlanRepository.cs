using System.Text.Json;
using Birko.Data.SQL.Repositories;
using Birko.Data.Stores;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Models.Agents;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQL-backed plan repository for immediate atomic persistence of plan state.
    /// Eliminates the debounce race condition by writing plan state directly to SQLite.
    /// File-based JSON/Markdown output remains as secondary human-readable output.
    /// </summary>
    public class SqlPlanRepository
    {
        private readonly AsyncSqLiteModelRepository<PlanEntity> _repository;
        private readonly ILogger<SqlPlanRepository>? _logger;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public SqlPlanRepository(string dbPath, ILogger<SqlPlanRepository>? logger = null)
        {
            _logger = logger;
            _repository = new AsyncSqLiteModelRepository<PlanEntity>();

            var dbDir = Path.GetDirectoryName(dbPath) ?? ".";
            var dbFile = Path.GetFileName(dbPath);
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            _repository.SetSettings(new PasswordSettings(dbDir, dbFile));
        }

        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
            _logger?.LogInformation("SQLite plan repository initialized");
        }

        /// <summary>
        /// Saves or updates a plan atomically in the database.
        /// </summary>
        public async Task SavePlanAsync(KoboldImplementationPlan plan)
        {
            var existing = await _repository.ReadAsync(
                e => e.TaskId == plan.TaskId && e.ProjectId == plan.ProjectId,
                CancellationToken.None);

            var planDataJson = JsonSerializer.Serialize(plan, JsonOptions);

            if (existing != null)
            {
                existing.PlanFilename = plan.PlanFilename;
                existing.TaskDescription = plan.TaskDescription;
                existing.Status = (int)plan.Status;
                existing.CurrentStepIndex = plan.CurrentStepIndex;
                existing.ErrorMessage = plan.ErrorMessage;
                existing.SpecificationVersion = plan.SpecificationVersion;
                existing.SpecificationContentHash = plan.SpecificationContentHash;
                existing.FeatureId = plan.FeatureId;
                existing.FeatureName = plan.FeatureName;
                existing.PlanUpdatedAt = DateTime.UtcNow;
                existing.PlanDataJson = planDataJson;
                existing.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(existing);
            }
            else
            {
                var entity = new PlanEntity
                {
                    TaskId = plan.TaskId,
                    ProjectId = plan.ProjectId,
                    PlanFilename = plan.PlanFilename,
                    TaskDescription = plan.TaskDescription,
                    Status = (int)plan.Status,
                    CurrentStepIndex = plan.CurrentStepIndex,
                    ErrorMessage = plan.ErrorMessage,
                    SpecificationVersion = plan.SpecificationVersion,
                    SpecificationContentHash = plan.SpecificationContentHash,
                    FeatureId = plan.FeatureId,
                    FeatureName = plan.FeatureName,
                    PlanCreatedAt = plan.CreatedAt,
                    PlanUpdatedAt = DateTime.UtcNow,
                    PlanDataJson = planDataJson,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _repository.CreateAsync(entity);
            }
        }

        /// <summary>
        /// Loads a plan from the database.
        /// </summary>
        public async Task<KoboldImplementationPlan?> LoadPlanAsync(string projectId, string taskId)
        {
            var entity = await _repository.ReadAsync(
                e => e.TaskId == taskId && e.ProjectId == projectId,
                CancellationToken.None);

            if (entity == null || string.IsNullOrEmpty(entity.PlanDataJson))
                return null;

            try
            {
                return JsonSerializer.Deserialize<KoboldImplementationPlan>(entity.PlanDataJson, JsonOptions);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to deserialize plan for task {TaskId}", taskId);
                return null;
            }
        }

        /// <summary>
        /// Deletes a plan from the database.
        /// </summary>
        public async Task DeletePlanAsync(string projectId, string taskId)
        {
            var entity = await _repository.ReadAsync(
                e => e.TaskId == taskId && e.ProjectId == projectId,
                CancellationToken.None);

            if (entity != null)
            {
                await _repository.DeleteAsync(entity);
            }
        }

        /// <summary>
        /// Gets all plans for a project.
        /// </summary>
        public async Task<List<KoboldImplementationPlan>> GetPlansForProjectAsync(string projectId)
        {
            var entities = await _repository.ReadAsync(
                e => e.ProjectId == projectId, orderBy: null, limit: null, offset: null);

            var plans = new List<KoboldImplementationPlan>();
            foreach (var entity in entities)
            {
                try
                {
                    var plan = JsonSerializer.Deserialize<KoboldImplementationPlan>(entity.PlanDataJson, JsonOptions);
                    if (plan != null) plans.Add(plan);
                }
                catch { /* skip corrupt entries */ }
            }

            return plans.OrderByDescending(p => p.UpdatedAt).ToList();
        }
    }
}
