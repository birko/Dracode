using System.Globalization;
using Birko.Data.EventSourcing.Events;
using Birko.Data.EventSourcing.SQL;
using Birko.Data.EventSourcing.SQL.Models;
using Birko.Data.SQL.Connectors;
using Birko.Data.SQL.SqLite.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Data
{
    /// <summary>
    /// The specification event store (TASK-127): Birko's <see cref="SqlEventStore{DB}"/> on the KoboldLair SQLite
    /// database (table <c>__Events</c>), and the one-time move of the events DraCode's own repository wrote to
    /// <c>domain_events</c> before it.
    /// </summary>
    public static class LegacyDomainEventMigration
    {
        public const string LegacyTable = "domain_events";

        /// <summary>The name the legacy table is kept under once its rows are in <c>__Events</c>.</summary>
        public const string MigratedTable = "domain_events_migrated";

        public static SqlEventStore<SqLiteConnector> CreateStore(string dbPath)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".";
            Directory.CreateDirectory(dir);
            var inner = new AsyncSQLiteStore<DomainEventModel>();
            inner.SetSettings(new SqLiteSettings(dir, Path.GetFileName(dbPath)));
            return new SqlEventStore<SqLiteConnector>(inner);
        }

        /// <summary>
        /// Copies every <c>domain_events</c> row into <paramref name="store"/> — same event id, aggregate, version,
        /// type, data, metadata, user and time — then keeps the table as <see cref="MigratedTable"/> (renamed, or
        /// merged into it when an earlier migration already made one). Returns how many events were copied.
        /// <para>
        /// When the store already has the (aggregate, version): the same event id means an earlier, interrupted run
        /// copied it, and it is skipped; a different id means a different event — a pre-TASK-127 server that ran
        /// after a migration numbers from its own empty table again — and it is appended at the aggregate's next
        /// version, so nothing is lost.
        /// </para>
        /// </summary>
        public static async Task<int> MigrateAsync(string dbPath, IAsyncEventStore store, ILogger? logger = null,
            CancellationToken cancellationToken = default)
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            if (!await TableExistsAsync(connection, LegacyTable, cancellationToken))
                return 0;

            var legacy = await ReadLegacyAsync(connection, cancellationToken);
            var copied = 0;
            foreach (var @event in legacy.OrderBy(e => e.OccurredAt).ThenBy(e => e.Version))
            {
                if (await TryAppendAsync(store, @event, cancellationToken))
                {
                    copied++;
                    continue;
                }

                var taken = (await store.ReadFromVersionAsync(@event.AggregateGuid, @event.Version, cancellationToken))
                    .First(e => e.Version == @event.Version);
                if (taken.EventGuid == @event.EventGuid)
                    continue; // copied by an earlier, interrupted run

                var original = @event.Version;
                do
                    @event.Version = await store.GetVersionAsync(@event.AggregateGuid, cancellationToken) + 1;
                while (!await TryAppendAsync(store, @event, cancellationToken));
                copied++;
                logger?.LogWarning(
                    "Event {EventGuid} of {AggregateGuid} was stored at version {Original}, which another event already has; moved to version {Version}",
                    @event.EventGuid, @event.AggregateGuid, original, @event.Version);
            }

            await using (var keep = connection.CreateCommand())
            {
                keep.CommandText = await TableExistsAsync(connection, MigratedTable, cancellationToken)
                    ? $"INSERT OR IGNORE INTO \"{MigratedTable}\" SELECT * FROM \"{LegacyTable}\"; DROP TABLE \"{LegacyTable}\";"
                    : $"ALTER TABLE \"{LegacyTable}\" RENAME TO \"{MigratedTable}\"";
                await keep.ExecuteNonQueryAsync(cancellationToken);
            }

            logger?.LogInformation(
                "Moved {Copied} of {Total} specification events from {Legacy} to the Birko event store; the old table is kept as {Migrated}",
                copied, legacy.Count, LegacyTable, MigratedTable);
            return copied;
        }

        private static async Task<bool> TryAppendAsync(IAsyncEventStore store, IEvent @event, CancellationToken cancellationToken)
        {
            try
            {
                await store.AppendAsync(@event, cancellationToken);
                return true;
            }
            catch (EventStoreConcurrencyException)
            {
                return false;
            }
        }

        private static async Task<bool> TableExistsAsync(SqliteConnection connection, string table, CancellationToken cancellationToken)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            cmd.Parameters.AddWithValue("$name", table);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync(cancellationToken)) > 0;
        }

        // The layout DraCode's DomainEventEntity wrote: Guid is the event id, OccurredAt a UTC time stored as text.
        private static async Task<List<DomainEvent>> ReadLegacyAsync(SqliteConnection connection, CancellationToken cancellationToken)
        {
            var events = new List<DomainEvent>();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText =
                $"SELECT Guid, AggregateId, Version, EventType, EventData, Metadata, UserId, OccurredAt FROM \"{LegacyTable}\"";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                events.Add(new DomainEvent
                {
                    EventGuid = Guid.Parse(reader.GetString(0)),
                    AggregateGuid = Guid.Parse(reader.GetString(1)),
                    Version = reader.GetInt64(2),
                    EventType = reader.GetString(3),
                    EventData = reader.IsDBNull(4) ? "{}" : reader.GetString(4),
                    Metadata = reader.IsDBNull(5) ? null : reader.GetString(5),
                    UserGuid = reader.IsDBNull(6) || string.IsNullOrEmpty(reader.GetString(6)) ? null : Guid.Parse(reader.GetString(6)),
                    OccurredAt = DateTime.SpecifyKind(
                        DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.None), DateTimeKind.Utc)
                });
            }
            return events;
        }
    }
}
