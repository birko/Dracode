using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// Write-Ahead Log entry for task state changes
    /// </summary>
    public record WalEntry(
        DateTime Timestamp,
        string TaskId,
        string PreviousStatus,
        string NewStatus,
        string? AssignedAgent = null,
        string? ErrorMessage = null);

    /// <summary>
    /// Write-Ahead Log for task state changes to prevent data loss during crashes.
    /// Appends critical state transitions to a log file before updating in-memory state.
    /// </summary>
    public class TaskStateWal : IDisposable
    {
        private readonly string _walPath;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly ILogger? _logger;
        private bool _disposed;

        public TaskStateWal(string taskFilePath, ILogger? logger = null)
        {
            _walPath = Path.ChangeExtension(taskFilePath, ".wal");
            _logger = logger;
        }

        /// <summary>
        /// Appends a state transition to the WAL
        /// </summary>
        public async Task AppendAsync(WalEntry entry)
        {
            if (_disposed) return;

            await _writeLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(entry);
                await File.AppendAllLinesAsync(_walPath, new[] { json });
                _logger?.LogDebug("WAL: {TaskId} {PreviousStatus} → {NewStatus}", 
                    entry.TaskId, entry.PreviousStatus, entry.NewStatus);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to append to WAL: {Path}", _walPath);
                throw;
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Reads all entries from the WAL for recovery
        /// </summary>
        public async Task<List<WalEntry>> ReadAllAsync()
        {
            if (!File.Exists(_walPath))
                return new List<WalEntry>();

            try
            {
                var lines = await File.ReadAllLinesAsync(_walPath);
                var entries = new List<WalEntry>();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var entry = JsonSerializer.Deserialize<WalEntry>(line);
                        if (entry != null)
                            entries.Add(entry);
                    }
                    catch (JsonException ex)
                    {
                        _logger?.LogWarning(ex, "Skipping malformed WAL entry: {Line}", line);
                    }
                }

                _logger?.LogInformation("Read {Count} entries from WAL: {Path}", entries.Count, _walPath);
                return entries;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to read WAL: {Path}", _walPath);
                return new List<WalEntry>();
            }
        }

        /// <summary>
        /// Clears the WAL after a successful checkpoint (task file save)
        /// </summary>
        public async Task CheckpointAsync()
        {
            if (!File.Exists(_walPath)) return;

            await _writeLock.WaitAsync();
            try
            {
                File.Delete(_walPath);
                _logger?.LogDebug("WAL checkpoint: cleared {Path}", _walPath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clear WAL after checkpoint: {Path}", _walPath);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        /// <summary>
        /// Checks if WAL exists and has entries (indicates incomplete writes)
        /// </summary>
        public bool HasUncommittedChanges()
        {
            return File.Exists(_walPath) && new FileInfo(_walPath).Length > 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _writeLock?.Dispose();
        }
    }
}
