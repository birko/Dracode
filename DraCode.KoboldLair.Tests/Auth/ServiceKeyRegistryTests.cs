using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Server.Auth;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DraCode.KoboldLair.Tests.Auth;

/// <summary>Service-key owners are kept on the configured backend's store factory (TASK-228), any backend alike.</summary>
public class ServiceKeyRegistryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_keys_{Guid.NewGuid():N}");

    public ServiceKeyRegistryTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    public static TheoryData<string> Backends => new() { "SqLite", "InMemory" };

    [Theory]
    [MemberData(nameof(Backends))]
    public async Task Keys_are_added_listed_by_owner_newest_first_and_read_back(string backend)
    {
        var factory = DataStores.Create(backend, backend == "SqLite" ? Path.Combine(_dir, "koboldlair.db") : "");
        var registry = new ServiceKeyRegistry(factory);
        var t = new DateTime(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);

        await registry.AddAsync(new ServiceKey("c1", "alice", "ci", t));
        await registry.AddAsync(new ServiceKey("c2", "alice", "deploy", t.AddMinutes(1)));
        await registry.AddAsync(new ServiceKey("c3", "bob", "backup", t.AddMinutes(2)));

        (await registry.ListAsync("alice")).Select(k => k.ClientId).Should().Equal("c2", "c1");
        (await registry.ListAsync(null)).Should().HaveCount(3);
        (await registry.GetAsync("c3"))!.OwnerId.Should().Be("bob");
        (await registry.GetAsync("missing")).Should().BeNull();
        (await registry.NameTakenAsync("DEPLOY")).Should().BeTrue();
    }

    [Fact]
    public async Task On_SQLite_a_second_registry_on_the_same_file_sees_the_keys()
    {
        var path = Path.Combine(_dir, "koboldlair.db");
        await new ServiceKeyRegistry(DataStores.SqLite(path)).AddAsync(new ServiceKey("c1", "alice", "ci", DateTime.UtcNow));

        (await new ServiceKeyRegistry(DataStores.SqLite(path)).GetAsync("c1")).Should().NotBeNull("the keys are in the database");
    }
}
