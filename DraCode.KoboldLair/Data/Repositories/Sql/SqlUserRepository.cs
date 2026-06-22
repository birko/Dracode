using Birko.Data.SQL.Repositories;
using Birko.Data.Stores;
using Birko.Configuration;
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
        private readonly AsyncSqLiteModelRepository<UserEntity> _repository;
        private readonly ILogger<SqlUserRepository>? _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public SqlUserRepository(string dbPath, ILogger<SqlUserRepository>? logger = null)
        {
            _logger = logger;
            _repository = new AsyncSqLiteModelRepository<UserEntity>();

            var dbDir = Path.GetDirectoryName(dbPath) ?? ".";
            var dbFile = Path.GetFileName(dbPath);
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            _repository.SetSettings(new PasswordSettings(dbDir, dbFile));
            _logger?.LogInformation("SQLite user repository initialized at: {Path}", dbPath);
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
