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
            var dbPath = ResolveSqLitePath(config);
            var logger = loggerFactory?.CreateLogger<SqlProjectRepository>();
            var repo = new SqlProjectRepository(dbPath, logger);
            await repo.InitializeAsync();
            return repo;
        }

        private static async Task<SqlTaskRepository> CreateSqlTaskRepositoryAsync(
            DataStorageConfig config, ILoggerFactory? loggerFactory)
        {
            var dbPath = ResolveSqLitePath(config);
            var logger = loggerFactory?.CreateLogger<SqlTaskRepository>();
            var repo = new SqlTaskRepository(dbPath, logger);
            await repo.InitializeAsync();
            return repo;
        }

        private static string ResolveSqLitePath(DataStorageConfig config)
        {
            if (Path.IsPathRooted(config.SqLitePath))
                return config.SqLitePath;

            return Path.Combine(config.ProjectsPath, config.SqLitePath);
        }
    }
}
