using System.Text.Json;
using Birko.Data.SQL.Repositories;
using Birko.Data.Stores;
using DraCode.KoboldLair.Data.Entities;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data.Repositories.Sql
{
    /// <summary>
    /// SQL-backed repository for Dragon conversation history.
    /// Replaces fire-and-forget file I/O with serialized database writes.
    /// Uses a per-project semaphore to prevent concurrent write races.
    /// </summary>
    public class SqlHistoryRepository
    {
        private readonly AsyncSqLiteModelRepository<DragonHistoryEntity> _repository;
        private readonly ILogger<SqlHistoryRepository>? _logger;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public SqlHistoryRepository(string dbPath, ILogger<SqlHistoryRepository>? logger = null)
        {
            _logger = logger;
            _repository = new AsyncSqLiteModelRepository<DragonHistoryEntity>();

            var dbDir = Path.GetDirectoryName(dbPath) ?? ".";
            var dbFile = Path.GetFileName(dbPath);
            if (!Directory.Exists(dbDir))
                Directory.CreateDirectory(dbDir);

            _repository.SetSettings(new PasswordSettings(dbDir, dbFile));
        }

        public async Task InitializeAsync()
        {
            await _repository.CreateSchemaAsync();
            _logger?.LogInformation("SQLite history repository initialized");
        }

        /// <summary>
        /// Saves message history for a project. Serialized via semaphore to prevent races.
        /// </summary>
        public async Task SaveHistoryAsync<T>(string projectFolder, List<T> messages)
        {
            await _writeLock.WaitAsync();
            try
            {
                var normalizedFolder = NormalizeFolder(projectFolder);
                var json = JsonSerializer.Serialize(messages, JsonOptions);

                var existing = await _repository.ReadAsync(
                    e => e.ProjectFolder == normalizedFolder, CancellationToken.None);

                if (existing != null)
                {
                    existing.MessagesJson = json;
                    existing.MessageCount = messages.Count;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _repository.UpdateAsync(existing);
                }
                else
                {
                    var entity = new DragonHistoryEntity
                    {
                        ProjectFolder = normalizedFolder,
                        MessagesJson = json,
                        MessageCount = messages.Count,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _repository.CreateAsync(entity);
                }
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Loads message history for a project.
        /// </summary>
        public async Task<string?> LoadHistoryJsonAsync(string projectFolder)
        {
            var normalizedFolder = NormalizeFolder(projectFolder);
            var entity = await _repository.ReadAsync(
                e => e.ProjectFolder == normalizedFolder, CancellationToken.None);

            return entity?.MessagesJson;
        }

        private static string NormalizeFolder(string folder) =>
            Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Replace('\\', '/');
    }
}
