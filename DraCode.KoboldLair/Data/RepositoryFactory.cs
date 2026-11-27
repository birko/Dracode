using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data
{
    /// <summary>
    /// Supported storage backends for repository creation.
    /// </summary>
    public enum StorageBackend
    {
        /// <summary>
        /// JSON file storage (legacy, default fallback)
        /// </summary>
        JsonFile,

        /// <summary>
        /// SQLite database via Birko.Data.SQL.SqLite
        /// </summary>
        SqLite
    }

    /// <summary>
    /// Configuration for data storage.
    /// </summary>
    public class DataStorageConfig
    {
        /// <summary>
        /// Default storage backend for all repositories.
        /// </summary>
        public StorageBackend DefaultBackend { get; set; } = StorageBackend.JsonFile;

        /// <summary>
        /// SQLite database file path. Relative to projects directory.
        /// </summary>
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
        /// Projects directory (for JSON backend or SQLite relative path).
        /// </summary>
        public string ProjectsPath { get; set; } = "./projects";
    }

    /// <summary>
    /// Factory for creating repository instances based on configured storage backend.
    /// Supports runtime switching between JSON file storage and SQLite database.
    /// </summary>
    public static class RepositoryFactory
    {
        /// <summary>
        /// Creates an IProjectRepository based on the configured backend.
        /// For SQLite, also initializes the database schema.
        /// </summary>
        public static async Task<IProjectRepository> CreateProjectRepositoryAsync(
            DataStorageConfig config,
            ILoggerFactory? loggerFactory = null)
        {
            return config.DefaultBackend switch
            {
                StorageBackend.SqLite => await CreateSqlProjectRepositoryAsync(config, loggerFactory),
                _ => throw new ArgumentException(
                    $"JSON backend not supported via factory. Use ProjectRepository directly. Backend: {config.DefaultBackend}")
            };
        }

        /// <summary>
        /// Creates an ITaskRepository based on the configured backend.
        /// For SQLite, also initializes the database schema.
        /// </summary>
        public static async Task<ITaskRepository> CreateTaskRepositoryAsync(
            DataStorageConfig config,
            ILoggerFactory? loggerFactory = null)
        {
            return config.DefaultBackend switch
            {
                StorageBackend.SqLite => await CreateSqlTaskRepositoryAsync(config, loggerFactory),
                _ => throw new ArgumentException(
                    $"JSON backend not supported via factory. Use TaskTracker directly. Backend: {config.DefaultBackend}")
            };
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
        /// Creates an IUserRepository based on the configured backend.
        /// For SQLite, also initializes the database schema.
        /// </summary>
        public static async Task<IUserRepository> CreateUserRepositoryAsync(
            DataStorageConfig config,
            ILoggerFactory? loggerFactory = null)
        {
            return config.DefaultBackend switch
            {
                StorageBackend.SqLite => await CreateSqlUserRepositoryAsync(config, loggerFactory),
                _ => throw new ArgumentException(
                    $"JSON backend not supported via factory for users. Backend: {config.DefaultBackend}")
            };
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
