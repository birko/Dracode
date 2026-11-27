using Birko.Data.SQL.Repositories;
using DraCode.KoboldLair.Data.Entities;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// Each person's workspace layouts (TASK-058) in <c>user_layouts</c> on the shared database: one current layout per
    /// owner and any number of named ones (the endpoint caps them), keyed by owner and name.
    /// </summary>
    public class SqlLayoutRepository
    {
        private readonly StoreRepository<UserLayoutEntity> _repository;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly ILogger? _logger;

        /// <summary>On the SQLite file at <paramref name="dbPath"/> (tests, and the default database).</summary>
        public SqlLayoutRepository(string dbPath, ILogger? logger = null)
            : this(DataStores.SqLite(dbPath), logger)
        {
        }

        /// <summary>On the configured backend (TASK-200).</summary>
        public SqlLayoutRepository(Birko.Data.Stores.IStoreFactory factory, ILogger? logger = null)
        {
            _repository = new StoreRepository<UserLayoutEntity>(factory);
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
            _logger?.LogInformation("SQLite layout repository initialized");
        }

        /// <summary>The owner's current layout's JSON, or null when they have none yet.</summary>
        public async Task<string?> GetCurrentAsync(string ownerId) =>
            (await _repository.ReadAsync(e => e.OwnerId == ownerId && e.IsCurrent, CancellationToken.None))?.LayoutJson;

        public Task SaveCurrentAsync(string ownerId, string layoutJson) => UpsertAsync(ownerId, string.Empty, isCurrent: true, layoutJson);

        /// <summary>The owner's named layouts, by name.</summary>
        public async Task<List<UserLayoutEntity>> ListAsync(string ownerId)
        {
            var rows = await _repository.ReadAsync(filter: e => e.OwnerId == ownerId && !e.IsCurrent, orderBy: null, limit: null, offset: null);
            return rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public async Task<bool> ExistsAsync(string ownerId, string name) =>
            await _repository.ReadAsync(e => e.OwnerId == ownerId && !e.IsCurrent && e.Name == name, CancellationToken.None) != null;

        public async Task<int> CountAsync(string ownerId) =>
            (int)await _repository.CountAsync(e => e.OwnerId == ownerId && !e.IsCurrent);

        /// <summary>Saves a named layout, replacing one of the same name; returns the stored row.</summary>
        public Task<UserLayoutEntity> SaveAsync(string ownerId, string name, string layoutJson) => UpsertAsync(ownerId, name, isCurrent: false, layoutJson);

        /// <summary>Deletes a named layout; false when the owner has none of that name.</summary>
        public async Task<bool> DeleteAsync(string ownerId, string name)
        {
            await _lock.WaitAsync();
            try
            {
                var row = await _repository.ReadAsync(e => e.OwnerId == ownerId && !e.IsCurrent && e.Name == name, CancellationToken.None);
                if (row == null) return false;
                await _repository.DeleteAsync(row);
                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<UserLayoutEntity> UpsertAsync(string ownerId, string name, bool isCurrent, string layoutJson)
        {
            await _lock.WaitAsync();
            try
            {
                var now = DateTime.UtcNow;
                var row = await _repository.ReadAsync(e => e.OwnerId == ownerId && e.IsCurrent == isCurrent && e.Name == name, CancellationToken.None);
                if (row == null)
                {
                    row = new UserLayoutEntity { OwnerId = ownerId, Name = name, IsCurrent = isCurrent, LayoutJson = layoutJson, CreatedAt = now, UpdatedAt = now };
                    await _repository.CreateAsync(row);
                }
                else
                {
                    row.LayoutJson = layoutJson;
                    row.UpdatedAt = now;
                    await _repository.UpdateAsync(row);
                }
                return row;
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
