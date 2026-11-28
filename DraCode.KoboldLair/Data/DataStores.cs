using Birko.Data.EventSourcing.Events;
using Birko.Data.EventSourcing.MongoDB;
using Birko.Data.EventSourcing.SQL;
using Birko.Data.EventSourcing.Stores;
using Birko.Data.InMemory.Stores;
using Birko.Data.Models;
using Birko.Data.MongoDB.Stores;
using Birko.Data.Repositories;
using Birko.Data.SQL.Connectors;
using Birko.Data.SQL.PostgreSQL.Stores;
using Birko.Data.SQL.SqLite.Stores;
using Birko.Data.SQL.Stores;
using Birko.Data.Stores;

namespace DraCode.KoboldLair.Data
{
    /// <summary>
    /// The one place DraCode's stores come from (TASK-200, FEATURE-078's sibling EPIC-016): a Birko <see cref="IStoreFactory"/>
    /// built from a backend name and a connection string, so the same repositories run on SQLite in tests and on
    /// PostgreSQL or MongoDB in production. SQLite is the default, on the given file.
    /// </summary>
    public static class DataStores
    {
        /// <summary>The backend used when none is configured.</summary>
        public const string DefaultBackend = "SqLite";

        /// <summary>
        /// The factory for <paramref name="backend"/> (<c>SqLite</c>, <c>PostgreSQL</c>, <c>MongoDB</c>, or <c>InMemory</c> for
        /// tests; any case) and its
        /// connection string: a file path for SQLite, an Npgsql connection string, or <c>mongodb://host:port/database</c>.
        /// An unknown backend or a connection string it cannot read is refused by Birko, naming what is wrong.
        /// </summary>
        public static IStoreFactory Create(string? backend, string connectionString) =>
            StoreFactory.Create(string.IsNullOrWhiteSpace(backend) ? DefaultBackend : backend, connectionString,
                providers => providers.AddSqLite().AddPostgreSQL().AddMongoDB().AddInMemory());

        /// <summary>A SQLite factory on the file at <paramref name="dbPath"/> (in WAL from first use, as every SQLite store).</summary>
        public static IStoreFactory SqLite(string dbPath) => Create(DefaultBackend, dbPath);

        /// <summary>Creates what <typeparamref name="T"/>'s store needs, where the backend can (<see cref="ISchemaStoreFactory"/>).</summary>
        public static Task CreateSchemaForAsync<T>(this IStoreFactory factory, CancellationToken ct = default) =>
            factory is ISchemaStoreFactory schema ? schema.CreateSchemaAsync(new[] { typeof(T) }, ct) : Task.CompletedTask;

        /// <summary>The SQL connector behind the factory, or null on a non-SQL backend (where tables have no fixed columns).</summary>
        public static AbstractConnector? SqlConnector(this IStoreFactory factory) =>
            factory is ISqlStoreFactory sql ? sql.GetConnector() : null;

        /// <summary>
        /// The specification event store on the factory's backend (TASK-228): Birko's SQL event store (<c>__Events</c>) on a
        /// SQL backend, its native MongoDB one on MongoDB, and its generic one over the factory's stores elsewhere (InMemory).
        /// </summary>
        public static IAsyncEventStore EventStore(this IStoreFactory factory) =>
            factory.GetEventStore(o => o.AddSql().AddMongoDB());
    }

    /// <summary>
    /// A repository over the configured backend's store for <typeparamref name="T"/> (TASK-200), with what DraCode's
    /// repositories used from the SQLite one: creating the schema, the SQL connector for column repair (null off SQL),
    /// and the store itself (for aggregation, where the store can).
    /// </summary>
    public sealed class StoreRepository<T> : AsyncStoreRepository<T> where T : AbstractModel
    {
        private readonly IStoreFactory _factory;

        public StoreRepository(IStoreFactory factory) : this(factory, factory.GetAsyncStore<T>())
        {
        }

        private StoreRepository(IStoreFactory factory, IAsyncBulkStore<T> store) : base(store)
        {
            _factory = factory;
            BulkStoreOf = store;
        }

        /// <summary>The store this repository works on.</summary>
        public IAsyncBulkStore<T> BulkStoreOf { get; }

        public Task CreateSchemaAsync(CancellationToken ct = default) => _factory.CreateSchemaForAsync<T>(ct);

        /// <summary>The SQL connector, or null on a non-SQL backend.</summary>
        public AbstractConnector? Connector => _factory.SqlConnector();
    }
}
