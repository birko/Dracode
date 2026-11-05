using Birko.Data.EventSourcing.Events;
using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Events.Specification;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services.EventSourcing;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// TASK-127: specification events go through Birko's SQL event store (<c>__Events</c>), and the events DraCode's own
/// repository wrote to <c>domain_events</c> move over at startup. The fixture is a dump of a <c>domain_events</c> table
/// written by that repository (commit a502718), in the layout of the real koboldlair.db.
/// </summary>
public class EventStoreMigrationTests : IDisposable
{
    private static readonly Guid Flappy = Guid.Parse("5d0b7c1e-3a2f-4e8d-9b6a-1c4f7e2d8a90");
    private static readonly Guid Presenter = Guid.Parse("a7e3f1c9-4b2d-4c6e-8f0a-2d9b5e7c1f34");
    private static readonly Guid User = Guid.Parse("0f1e2d3c-4b5a-6978-8a9b-0c1d2e3f4a5b");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_events_{Guid.NewGuid():N}");
    private readonly string _dbPath;

    public EventStoreMigrationTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "koboldlair.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private void Execute(string sql)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private long Count(string table)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{table}'";
        if ((long)cmd.ExecuteScalar()! == 0) return -1;
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
        return (long)cmd.ExecuteScalar()!;
    }

    private void LoadLegacyFixture() =>
        Execute(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyEvents", "domain_events.sql")));

    [Fact]
    public async Task Legacy_events_are_moved_into_the_Birko_store_with_their_ids_versions_times_and_users()
    {
        LoadLegacyFixture();
        var store = LegacyDomainEventMigration.CreateStore(_dbPath);

        var copied = await LegacyDomainEventMigration.MigrateAsync(_dbPath, store);

        copied.Should().Be(5);
        var flappy = (await store.ReadAsync(Flappy)).ToList();
        flappy.Select(e => (e.Version, e.EventType)).Should().Equal(
            (1L, "SpecificationCreated"), (2L, "FeatureAdded"), (3L, "FeatureAdded"), (4L, "SpecificationUpdated"));
        flappy[0].EventGuid.Should().Be(Guid.Parse("00000000-0000-4000-8000-5d0b00000001"));
        flappy[0].OccurredAt.Should().Be(new DateTime(2026, 10, 4, 8, 57, 34, 55, DateTimeKind.Utc).AddTicks(3807));
        flappy[0].OccurredAt.Kind.Should().Be(DateTimeKind.Utc);
        flappy[0].UserGuid.Should().Be(User);
        flappy[0].EventData.Should().Contain("\"projectName\":\"flappy-dragon\"");
        flappy[1].Metadata.Should().Be("{\"source\":\"dragon\"}");
        flappy[3].UserGuid.Should().BeNull();
        (await store.GetVersionAsync(Flappy)).Should().Be(4);
        (await store.ReadAsync(Presenter)).Should().ContainSingle();

        Count(LegacyDomainEventMigration.LegacyTable).Should().Be(-1, "the legacy table is renamed once moved");
        Count(LegacyDomainEventMigration.MigratedTable).Should().Be(5, "and kept, not dropped");
        (await LegacyDomainEventMigration.MigrateAsync(_dbPath, store)).Should().Be(0, "a second start has nothing to move");
    }

    [Fact]
    public async Task A_run_interrupted_after_part_of_the_copy_completes_without_duplicating_events()
    {
        LoadLegacyFixture();
        var store = LegacyDomainEventMigration.CreateStore(_dbPath);
        // Copied before the crash: the same event, with its own id
        await store.AppendAsync(new DomainEvent(Flappy, 1, "SpecificationCreated", "{}")
        {
            EventGuid = Guid.Parse("00000000-0000-4000-8000-5d0b00000001")
        });

        var copied = await LegacyDomainEventMigration.MigrateAsync(_dbPath, store);

        copied.Should().Be(4);
        (await store.ReadAsync(Flappy)).Select(e => e.Version).Should().Equal(1, 2, 3, 4);
        Count(LegacyDomainEventMigration.LegacyTable).Should().Be(-1);
    }

    [Fact]
    public async Task Events_an_old_server_wrote_after_a_migration_are_moved_too_and_none_is_lost()
    {
        LoadLegacyFixture();
        var store = LegacyDomainEventMigration.CreateStore(_dbPath);
        await LegacyDomainEventMigration.MigrateAsync(_dbPath, store);

        // A pre-TASK-127 server starts on the migrated database: it recreates domain_events and, reading its versions
        // from that empty table, writes the next flappy event as version 1 again.
        Execute("""
            CREATE TABLE "domain_events" (AggregateId TEXT NOT NULL, Version INTEGER NOT NULL, EventType TEXT NOT NULL, EventData TEXT, Metadata TEXT, UserId TEXT, OccurredAt INTEGER NOT NULL, Guid TEXT PRIMARY KEY UNIQUE, CreatedAt INTEGER NOT NULL, UpdatedAt INTEGER NOT NULL, PrevUpdatedAt INTEGER);
            INSERT INTO domain_events VALUES('5d0b7c1e-3a2f-4e8d-9b6a-1c4f7e2d8a90',1,'FeatureAdded','{"featureId":"FEATURE-003"}',NULL,NULL,'2026-11-05 14:03:00.0000000','11111111-2222-4333-8444-555555555555','2026-11-05 14:03:00.0000000','2026-11-05 14:03:00.0000000',NULL);
            """);

        var copied = await LegacyDomainEventMigration.MigrateAsync(_dbPath, store);

        copied.Should().Be(1);
        var late = (await store.ReadAsync(Flappy)).Last();
        late.Version.Should().Be(5, "version 1 is taken by another event, so it goes after the aggregate's last");
        late.EventGuid.Should().Be(Guid.Parse("11111111-2222-4333-8444-555555555555"));
        Count(LegacyDomainEventMigration.LegacyTable).Should().Be(-1);
        Count(LegacyDomainEventMigration.MigratedTable).Should().Be(6, "the kept table has every legacy row");
    }

    [Fact]
    public async Task A_database_without_legacy_events_is_left_alone()
    {
        var store = LegacyDomainEventMigration.CreateStore(_dbPath);

        (await LegacyDomainEventMigration.MigrateAsync(_dbPath, store)).Should().Be(0);
        Count(LegacyDomainEventMigration.MigratedTable).Should().Be(-1);
    }

    [Fact]
    public async Task Specification_events_are_written_to_and_read_from_the_Birko_store()
    {
        var service = new SpecificationEventService(LegacyDomainEventMigration.CreateStore(_dbPath));

        await service.RecordSpecificationCreatedAsync("spec-1", "flappy-dragon", "# Flappy", "flappy-dragon");
        await service.RecordFeatureAddedAsync("spec-1", new Feature { Id = "FEATURE-001", Name = "Flight and camera" });

        var history = (await service.GetSpecificationHistoryAsync("spec-1")).ToList();
        history.Select(h => (h.Version, h.EventType)).Should().Equal(
            (1L, SpecificationEventTypes.Created), (2L, SpecificationEventTypes.FeatureAdded));
        Count("__Events").Should().Be(2);
        Count(LegacyDomainEventMigration.LegacyTable).Should().Be(-1, "nothing writes the old table any more");
    }

    [Fact]
    public async Task Two_writers_changing_one_specification_at_once_both_land_on_distinct_versions()
    {
        // Two store instances = two writers that each read the version and append (as two processes would)
        var a = new SpecificationEventService(LegacyDomainEventMigration.CreateStore(_dbPath));
        var b = new SpecificationEventService(LegacyDomainEventMigration.CreateStore(_dbPath));
        await a.RecordSpecificationCreatedAsync("spec-1", "s", "# S", "p");

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(() => (i % 2 == 0 ? a : b)
            .RecordFeatureAddedAsync("spec-1", new Feature { Id = $"F-{i}", Name = $"feature {i}" }))));

        var versions = (await a.GetSpecificationHistoryAsync("spec-1")).Select(h => h.Version).ToList();
        versions.Should().Equal(Enumerable.Range(1, 11).Select(v => (long)v), "no version is lost or written twice");
    }
}
