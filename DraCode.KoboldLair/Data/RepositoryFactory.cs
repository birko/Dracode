using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data
{
    /// <summary>
    /// Configuration for data storage.
    /// </summary>
    public class DataStorageConfig
    {
        /// <summary>
        /// The setting that chose the removed JSON backend (TASK-227). Only <c>SqLite</c> is still accepted here, as the
        /// default it names; anything else refuses to start (<see cref="EnsureSupported"/>). Use <see cref="Provider"/>.
        /// </summary>
        public string? DefaultBackend { get; set; }

        /// <summary>
        /// The database every store uses (TASK-200): <c>SqLite</c> (the default), <c>PostgreSQL</c> or <c>MongoDB</c>, read
        /// from <c>KoboldLair:Data:Provider</c>.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Its connection string (<c>KoboldLair:Data:ConnectionString</c>): an Npgsql string, <c>mongodb://host:port/db</c>,
        /// or a SQLite file; for SQLite without one, <see cref="SqLitePath"/> under <see cref="ProjectsPath"/>.
        /// </summary>
        public string? ConnectionString { get; set; }

        public string SqLitePath { get; set; } = "koboldlair.db";

        /// <summary>
        /// Projects directory (where a relative SQLite path lives).
        /// </summary>
        public string ProjectsPath { get; set; } = "./projects";

        /// <summary>
        /// Refuses a configuration that still names the removed JSON backend (TASK-227), saying what to set instead.
        /// </summary>
        public void EnsureSupported()
        {
            if (!string.IsNullOrWhiteSpace(DefaultBackend)
                && !DefaultBackend.Trim().Equals(DataStores.DefaultBackend, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"KoboldLair:Data:DefaultBackend '{DefaultBackend}' is no longer supported: the JSON backend was removed and " +
                    "every store needs a database. Remove KoboldLair:Data:DefaultBackend and set KoboldLair:Data:Provider " +
                    "(SqLite, the default; PostgreSQL; MongoDB) and, off SQLite, KoboldLair:Data:ConnectionString.");
        }
    }

    /// <summary>
    /// Creates the repositories on the configured database (TASK-200, TASK-227).
    /// </summary>
    public static class RepositoryFactory
    {
        /// <summary>
        /// Creates the project repository on the configured database and initializes its schema.
        /// </summary>
        public static async Task<IProjectRepository> CreateProjectRepositoryAsync(
            DataStorageConfig config,
            ILoggerFactory? loggerFactory = null)
        {
            return await CreateSqlProjectRepositoryAsync(config, loggerFactory);
        }

        /// <summary>
        /// Creates the task repository on the configured database and initializes its schema.
        /// </summary>
        public static async Task<ITaskRepository> CreateTaskRepositoryAsync(
            DataStorageConfig config,
            ILoggerFactory? loggerFactory = null)
        {
            return await CreateSqlTaskRepositoryAsync(config, loggerFactory);
        }

        private static async Task<SqlProjectRepository> CreateSqlProjectRepositoryAsync(
            DataStorageConfig config, ILoggerFactory? loggerFactory)
        {
            var logger = loggerFactory?.CreateLogger<SqlProjectRepository>();
            var repo = new SqlProjectRepository(StoreFactoryFor(config), logger);
            await repo.InitializeAsync();
            return repo;
        }

        /// <summary>
        /// Creates the user repository on the configured database and initializes its schema.
        /// </summary>
        public static async Task<IUserRepository> CreateUserRepositoryAsync(
            DataStorageConfig config,
            ILoggerFactory? loggerFactory = null)
        {
            return await CreateSqlUserRepositoryAsync(config, loggerFactory);
        }

        private static async Task<SqlUserRepository> CreateSqlUserRepositoryAsync(
            DataStorageConfig config, ILoggerFactory? loggerFactory)
        {
            var logger = loggerFactory?.CreateLogger<SqlUserRepository>();
            var repo = new SqlUserRepository(StoreFactoryFor(config), logger);
            await repo.InitializeAsync();
            return repo;
        }

        private static async Task<SqlTaskRepository> CreateSqlTaskRepositoryAsync(
            DataStorageConfig config, ILoggerFactory? loggerFactory)
        {
            var logger = loggerFactory?.CreateLogger<SqlTaskRepository>();
            var repo = new SqlTaskRepository(StoreFactoryFor(config), logger);
            await repo.InitializeAsync();
            return repo;
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Birko.Data.Stores.IStoreFactory> Factories =
            new(StringComparer.Ordinal);

        /// <summary>
        /// The store factory for the configured database (TASK-200), one per database for the process: every store on it
        /// shares it (a MongoDB factory holds its client). SQLite on <see cref="ResolveSqLitePath"/> when nothing names
        /// another connection.
        /// </summary>
        public static Birko.Data.Stores.IStoreFactory StoreFactoryFor(DataStorageConfig config)
        {
            config.EnsureSupported();
            var backend = string.IsNullOrWhiteSpace(config.Provider) ? DataStores.DefaultBackend : config.Provider.Trim();
            var connection = !string.IsNullOrWhiteSpace(config.ConnectionString)
                ? config.ConnectionString
                : backend.Equals(DataStores.DefaultBackend, StringComparison.OrdinalIgnoreCase)
                    ? Path.GetFullPath(ResolveSqLitePath(config))
                    : throw new InvalidOperationException(
                        $"KoboldLair:Data:ConnectionString is required for the {backend} database.");
            return Factories.GetOrAdd($"{backend.ToLowerInvariant()}|{connection}", _ => DataStores.Create(backend, connection));
        }

        public static string ResolveSqLitePath(DataStorageConfig config)
        {
            if (Path.IsPathRooted(config.SqLitePath))
                return config.SqLitePath;

            return Path.Combine(config.ProjectsPath, config.SqLitePath);
        }
    }
}
