using System.Data.Common;
using System.Reflection;
using Birko.AI.Orchestration.Models;
using Birko.Configuration;
using Birko.Data.SQL;
using Birko.Data.SQL.Connectors;
using Birko.Data.SQL.Repositories;
using Birko.Data.SQL.SqLite.Stores;
using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// TASK-177: the KoboldLair database runs in WAL. On the rollback journal a pooled handle whose schema image predates a
/// table cannot re-check it while another connection holds the write lock, so its read fails with "no such table" and
/// Birko answers that as an empty result.
/// </summary>
/// <remarks>
/// Runs alone: other test classes call <c>SqliteConnection.ClearAllPools()</c> when they finish, which would drop the stale
/// pooled handle mid-test, and a fresh handle on the rollback journal then waits out the busy timeout on the write lock.
/// </remarks>
[Collection(SqliteJournalModeCollection.Name)]
public class SqliteJournalModeTests : IAsyncLifetime
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_wal_{Guid.NewGuid():N}");

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_dir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private string NewDbPath(string name) => Path.Combine(_dir, $"{name}-{Guid.NewGuid():N}.db");

    private static PendingDecision Decision() => new()
    {
        ProjectId = "proj-1",
        TaskId = "task-1",
        Disposition = EscalationDisposition.HumanDecisionRequired,
        Source = PendingDecisionSource.AskHuman,
        Question = "{\"text\":\"Which language?\"}",
        CreatedAt = new DateTime(2026, 11, 19, 10, 0, 0, DateTimeKind.Utc)
    };

    /// <summary>
    /// Arranges the read the task is about: the repository's store is initialized, the <c>pending_decisions</c> table
    /// and one decision are written through a connection outside the pool, the pool's only idle handle loaded the
    /// schema before that table existed, and another connection holds the write lock. Dispose to release it all.
    /// </summary>
    /// <remarks>
    /// The stale handle is kept checked out while the store initializes, so the store's own handle is a second one;
    /// that one is then held by <see cref="StaleRead.Holder"/>, which leaves the stale handle the only one a read can
    /// take from the pool. The rollback test's empty result is what shows the read landed on it.
    /// </remarks>
    private async Task<StaleRead> ArrangeStaleReadAsync(
        SqLiteConnector connector, PasswordSettings settings, string dbPath, Func<Task> initializeStore)
    {
        // Seed the table and a row in a file of their own, so the target's DDL is exactly the repository's
        var seedPath = NewDbPath("seed");
        var seed = new SqlPendingDecisionRepository(seedPath);
        await seed.InitializeAsync();
        await seed.CreateAsync(Decision());

        var stale = connector.CreateConnection(settings);
        stale.Open();
        Scalar(stale, "SELECT count(*) FROM sqlite_master").Should().Be(0L, "the file has no tables yet");

        using (var outside = Unpooled(dbPath))
        {
            Execute(outside, $"ATTACH DATABASE '{seedPath}' AS seed");
            var statements = new List<string>();
            using (var ddl = outside.CreateCommand())
            {
                ddl.CommandText = "SELECT sql FROM seed.sqlite_master WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%' ORDER BY type = 'index'";
                using var reader = ddl.ExecuteReader();
                while (reader.Read()) statements.Add(reader.GetString(0));
            }
            foreach (var sql in statements) Execute(outside, sql);
            Execute(outside, "INSERT INTO main.pending_decisions SELECT * FROM seed.pending_decisions");
            Execute(outside, "DETACH DATABASE seed");
        }

        await initializeStore();

        var holder = connector.CreateConnection(settings);
        holder.Open();
        stale.Dispose();

        var writer = Unpooled(dbPath);
        Execute(writer, "BEGIN EXCLUSIVE");
        return new StaleRead(writer, holder);
    }

    private sealed class StaleRead(SqliteConnection writer, DbConnection holder) : IDisposable
    {
        public DbConnection Holder => holder;

        public void Dispose()
        {
            Execute(writer, "ROLLBACK");
            writer.Dispose();
            holder.Dispose();
        }
    }

    [Fact]
    public async Task On_the_rollback_journal_a_stale_pooled_handle_reads_an_existing_table_as_empty()
    {
        // Asked for explicitly: since Birko TASK-941 plain PasswordSettings get WAL too. Off WAL (an explicit DELETE, or a
        // file where WAL cannot take) the read still answers empty; Birko TASK-942 tracks that.
        var dbPath = NewDbPath("rollback");
        var settings = new SqLiteSettings(_dir, Path.GetFileName(dbPath)) { JournalMode = "DELETE" };
        var repository = new AsyncSqLiteModelRepository<PendingDecisionEntity>();
        repository.SetSettings(settings);
        var connector = (SqLiteConnector)DataBase.GetConnector<SqLiteConnector>(settings);

        List<PendingDecisionEntity> rows;
        using (await ArrangeStaleReadAsync(connector, settings, dbPath, async () =>
               {
                   await repository.CreateSchemaAsync();
                   await repository.ReadAsync(filter: e => e.ProjectId == "none", orderBy: null, limit: null, offset: null);
               }))
        {
            rows = (await repository.ReadAsync(filter: e => e.ProjectId == "proj-1", orderBy: null, limit: null, offset: null)).ToList();
        }

        JournalMode(dbPath).Should().Be("delete");
        rows.Should().BeEmpty("the stale handle cannot see the table while the write lock is held, and a missing table reads as empty");
        (await repository.ReadAsync(filter: e => e.ProjectId == "proj-1", orderBy: null, limit: null, offset: null))
            .Should().ContainSingle("once the lock is released the same read finds the row");
    }

    [Fact]
    public async Task In_WAL_the_same_read_returns_the_rows()
    {
        var dbPath = NewDbPath("wal");
        var decisions = new SqlPendingDecisionRepository(dbPath);
        var settings = SqliteDatabaseSettings.For(dbPath);
        var connector = (SqLiteConnector)DataBase.GetConnector<SqLiteConnector>(settings);

        List<PendingDecision> open;
        using (await ArrangeStaleReadAsync(connector, settings, dbPath, async () =>
               {
                   await decisions.InitializeAsync();
                   await decisions.GetUnresolvedForProjectAsync("none");
               }))
        {
            open = await decisions.GetUnresolvedForProjectAsync("proj-1");
        }

        open.Should().ContainSingle("in WAL the stale handle re-checks the schema and finds the table").Which.TaskId.Should().Be("task-1");
        connector.JournalModeInEffect.Should().Be("wal");
    }

    /// <summary>Every <c>Sql*Repository</c>: a public type in that namespace whose constructor takes the database path first.</summary>
    public static IEnumerable<object[]> Repositories() =>
        typeof(SqlTaskRepository).Assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsPublic: true } && t.Namespace == typeof(SqlTaskRepository).Namespace)
            .Where(t => t.GetConstructors().Any(IsDbPathConstructor))
            .OrderBy(t => t.Name)
            .Select(t => new object[] { t.Name });

    private static bool IsDbPathConstructor(ConstructorInfo c)
    {
        var p = c.GetParameters();
        return p.Length > 0 && p[0].ParameterType == typeof(string) && p[0].Name == "dbPath";
    }

    [Fact]
    public void The_repository_list_covers_the_known_repositories()
    {
        Repositories().Select(r => (string)r[0]).Should().Contain(new[]
        {
            nameof(SqlBudgetRepository), nameof(SqlHistoryRepository), nameof(SqlPendingDecisionRepository), nameof(SqlPlanRepository),
            nameof(SqlProjectRepository), nameof(SqlProviderConfigRepository), nameof(SqlTaskRepository),
            nameof(SqlUsageRepository), nameof(SqlUserRepository)
        });
    }

    [Theory]
    [MemberData(nameof(Repositories))]
    public async Task Each_repository_built_first_on_a_fresh_file_leaves_it_in_WAL(string repositoryName)
    {
        var type = typeof(SqlTaskRepository).Assembly.GetTypes()
            .Single(t => t.Name == repositoryName && t.Namespace == typeof(SqlTaskRepository).Namespace);
        var dbPath = NewDbPath(repositoryName);
        var ctor = type.GetConstructors().First(IsDbPathConstructor);
        var args = ctor.GetParameters().Select((p, i) => i == 0 ? dbPath : LoggerFor(p.ParameterType)).ToArray();
        var repository = ctor.Invoke(args);

        var initialize = type.GetMethod("InitializeAsync", Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{repositoryName} has no InitializeAsync()");
        await (Task)initialize.Invoke(repository, null)!;

        JournalMode(dbPath).Should().Be("wal");
    }

    [Fact]
    public async Task The_event_store_leaves_a_fresh_file_in_WAL()
    {
        var dbPath = NewDbPath("events");
        var store = LegacyDomainEventMigration.CreateStore(dbPath);
        await store.ReadAsync(Guid.NewGuid());

        JournalMode(dbPath).Should().Be("wal");
    }

    private static object? LoggerFor(Type parameterType)
    {
        if (parameterType == typeof(ILogger)) return NullLogger.Instance;
        if (parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(ILogger<>))
            return Activator.CreateInstance(typeof(NullLogger<>).MakeGenericType(parameterType.GetGenericArguments()));
        throw new InvalidOperationException($"Unexpected repository constructor parameter {parameterType}");
    }

    private static SqliteConnection Unpooled(string dbPath)
    {
        var connection = new SqliteConnection($"Data Source={dbPath};Pooling=False");
        connection.Open();
        return connection;
    }

    private static string JournalMode(string dbPath)
    {
        using var connection = Unpooled(dbPath);
        return (string)Scalar(connection, "PRAGMA journal_mode")!;
    }

    private static object? Scalar(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public class SqliteJournalModeCollection
{
    public const string Name = "SQLite journal mode (runs alone)";
}
