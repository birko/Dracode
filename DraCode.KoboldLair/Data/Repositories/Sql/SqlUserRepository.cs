using Birko.Data.SQL.Repositories;
using Birko.Data.Stores;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Models.Users;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQL-backed user repository using Birko.Data with SQLite. Shares the same database file
    /// as the other SQL repositories. Keyed on the stable `sub`.
    /// </summary>
    public class SqlUserRepository : IUserRepository
    {
        private readonly StoreRepository<UserEntity> _repository;
        private readonly ILogger<SqlUserRepository>? _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        /// <summary>On the SQLite file at <paramref name="dbPath"/> (tests, and the default database).</summary>
        public SqlUserRepository(string dbPath, ILogger<SqlUserRepository>? logger = null)
            : this(DataStores.SqLite(dbPath), logger)
        {
        }

        /// <summary>On the configured backend (TASK-200).</summary>
        public SqlUserRepository(Birko.Data.Stores.IStoreFactory factory, ILogger<SqlUserRepository>? logger = null)
        {
            _repository = new StoreRepository<UserEntity>(factory);
            _logger = logger;
            _logger?.LogInformation("User repository on {Backend}", factory.Backend);
        }

        /// <summary>Ensures the database schema is created. Must be called once at startup.</summary>
        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
        }

        public async Task<User?> GetBySubAsync(string sub)
        {
            var entity = await _repository.ReadAsync(e => e.Sub == sub, CancellationToken.None);
            return entity == null ? null : EntityMapper.ToUser(entity);
        }

        public async Task UpsertAsync(User user)
        {
            if (string.IsNullOrEmpty(user.Sub))
                throw new ArgumentException("User.Sub is required", nameof(user));

            await _writeLock.WaitAsync();
            try
            {
                var existing = await _repository.ReadAsync(e => e.Sub == user.Sub, CancellationToken.None);
                if (existing == null)
                {
                    await _repository.CreateAsync(EntityMapper.ToEntity(user));
                    _logger?.LogInformation("Created user: {Sub}", user.Sub);
                }
                else
                {
                    EntityMapper.UpdateEntity(existing, user);
                    await _repository.UpdateAsync(existing);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }
    }
}
