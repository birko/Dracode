using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// One database setting builds the store factory every repository uses (TASK-200): SQLite on today's file by default, the
/// configured backend otherwise; one factory per database for the process; and the repositories work off SQL, where there
/// is no schema to create and no column to repair.
/// </summary>
public class StoreFactoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_storefactory_{Guid.NewGuid():N}");

    public StoreFactoryTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void With_no_provider_it_is_SQLite_on_the_configured_file_and_one_factory_per_database()
    {
        var config = new DataStorageConfig { ProjectsPath = _dir, SqLitePath = "koboldlair.db" };

        var factory = RepositoryFactory.StoreFactoryFor(config);

        factory.Backend.Should().BeEquivalentTo("SqLite");
        factory.Settings.GetId().Should().Contain("koboldlair.db");
        RepositoryFactory.StoreFactoryFor(new DataStorageConfig { ProjectsPath = _dir, SqLitePath = "koboldlair.db" })
            .Should().BeSameAs(factory, "every store on one database shares its factory");
        factory.SqlConnector().Should().NotBeNull();
    }

    [Fact]
    public void Another_database_needs_its_connection_string_and_an_unknown_one_is_refused()
    {
        var noConnection = () => RepositoryFactory.StoreFactoryFor(new DataStorageConfig { ProjectsPath = _dir, Provider = "PostgreSQL" });
        noConnection.Should().Throw<InvalidOperationException>().WithMessage("*KoboldLair:Data:ConnectionString*PostgreSQL*");

        var unknown = () => RepositoryFactory.StoreFactoryFor(new DataStorageConfig { ProjectsPath = _dir, Provider = "Oracle", ConnectionString = "x" });
        unknown.Should().Throw<Exception>().WithMessage("*Oracle*");
    }

    [Fact]
    public async Task Off_SQL_the_repositories_create_no_schema_repair_no_columns_and_still_read_and_write()
    {
        var factory = DataStores.Create("InMemory", "");
        factory.SqlConnector().Should().BeNull("an in-memory store has no SQL connector");

        var tasks = new SqlTaskRepository(factory);
        await tasks.InitializeAsync();   // column repair is skipped, not failed
        var decisions = new SqlPendingDecisionRepository(factory);
        await decisions.InitializeAsync();
        var usage = new SqlUsageRepository(factory, NullLogger.Instance);
        await usage.InitializeAsync();

        var decision = new PendingDecision { ProjectId = "p", TaskId = "", Source = PendingDecisionSource.AskHuman, Question = "{}" };
        await decisions.CreateAsync(decision);
        (await decisions.GetByIdAsync(decision.Id)).Should().NotBeNull();

        var now = DateTime.UtcNow;
        await usage.RecordUsageAsync(new UsageRecordEntity { Provider = "lan2", Model = "m", TotalTokens = 10, EstimatedCostUsd = 0.5, ProjectId = "p", RecordedAt = now });
        await usage.RecordUsageAsync(new UsageRecordEntity { Provider = "lan2", Model = "m", TotalTokens = 5, EstimatedCostUsd = 0.25, ProjectId = "p", RecordedAt = now });
        var byProvider = await usage.GetUsageByProviderAsync(now.AddHours(-1), now.AddHours(1));
        byProvider.Should().ContainSingle(r => r.Provider == "lan2" && r.TotalRequests == 2 && r.TotalTokens == 15);
        (await usage.GetProjectSpendAsync("p")).Should().BeApproximately(0.75, 1e-9);
    }
}
