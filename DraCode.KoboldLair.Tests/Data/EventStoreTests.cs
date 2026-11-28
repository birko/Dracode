using Birko.Data.EventSourcing.SQL;
using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Events.Specification;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services.EventSourcing;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// The specification event store comes from the configured backend's store factory (TASK-228): Birko's SQL event store
/// (<c>__Events</c>) on a SQL backend, Birko's generic or native one elsewhere. Two writers of one specification never
/// share a version.
/// </summary>
public class EventStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_events_{Guid.NewGuid():N}");
    private readonly string _dbPath;

    public EventStoreTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "koboldlair.db");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
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

    [Fact]
    public async Task On_SQLite_specification_events_go_through_Birkos_SQL_event_store()
    {
        var store = DataStores.SqLite(_dbPath).EventStore();
        var service = new SpecificationEventService(store);

        await service.RecordSpecificationCreatedAsync("spec-1", "flappy-dragon", "# Flappy", "flappy-dragon");
        await service.RecordFeatureAddedAsync("spec-1", new Feature { Id = "FEATURE-001", Name = "Flight and camera" });

        store.GetType().GetGenericTypeDefinition().Should().Be(typeof(SqlEventStore<>));
        var history = (await service.GetSpecificationHistoryAsync("spec-1")).ToList();
        history.Select(h => (h.Version, h.EventType)).Should().Equal(
            (1L, SpecificationEventTypes.Created), (2L, SpecificationEventTypes.FeatureAdded));
        Count("__Events").Should().Be(2);
    }

    [Fact]
    public async Task Off_SQL_the_backend_answers_an_event_store_too()
    {
        var service = new SpecificationEventService(DataStores.Create("InMemory", "").EventStore());

        await service.RecordSpecificationCreatedAsync("spec-1", "s", "# S", "p");
        await service.RecordFeatureAddedAsync("spec-1", new Feature { Id = "F-1", Name = "one" });

        (await service.GetSpecificationHistoryAsync("spec-1")).Select(h => h.Version).Should().Equal(1L, 2L);
    }

    [Fact]
    public async Task Many_writers_at_once_on_one_specification_all_land()
    {
        // Each round of a conflict one writer wins, so twenty at once need more than a handful of attempts (TASK-228)
        var factory = DataStores.Create("InMemory", "");
        var writers = Enumerable.Range(0, 20).Select(_ => new SpecificationEventService(factory.EventStore())).ToList();
        await writers[0].RecordSpecificationCreatedAsync("spec-1", "s", "# S", "p");

        await Task.WhenAll(writers.Select((w, i) => Task.Run(() =>
            w.RecordFeatureAddedAsync("spec-1", new Feature { Id = $"F-{i}", Name = $"feature {i}" }))));

        (await writers[0].GetSpecificationHistoryAsync("spec-1")).Select(h => h.Version)
            .Should().Equal(Enumerable.Range(1, 21).Select(v => (long)v));
    }

    [Fact]
    public async Task Two_writers_changing_one_specification_at_once_both_land_on_distinct_versions()
    {
        // Two factories = two writers that each read the version and append (as two processes would)
        var a = new SpecificationEventService(DataStores.SqLite(_dbPath).EventStore());
        var b = new SpecificationEventService(DataStores.SqLite(_dbPath).EventStore());
        await a.RecordSpecificationCreatedAsync("spec-1", "s", "# S", "p");

        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(() => (i % 2 == 0 ? a : b)
            .RecordFeatureAddedAsync("spec-1", new Feature { Id = $"F-{i}", Name = $"feature {i}" }))));

        var versions = (await a.GetSpecificationHistoryAsync("spec-1")).Select(h => h.Version).ToList();
        versions.Should().Equal(Enumerable.Range(1, 11).Select(v => (long)v), "no version is lost or written twice");
    }
}
