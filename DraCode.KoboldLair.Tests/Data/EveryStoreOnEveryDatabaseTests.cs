using Birko.Data.Stores;
using DraCode.KoboldLair.Data;
using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Models.Users;
using DraCode.KoboldLair.Server.Auth;
using DraCode.KoboldLair.Services.EventSourcing;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using Npgsql;
using Xunit.Abstractions;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// Every store opens, writes and reads back on every database KoboldLair can be configured for (TASK-229, the last slice
/// of TASK-200). SQLite and InMemory always run. PostgreSQL and MongoDB follow Birko's live-test convention: they run
/// against a fresh database when <c>BIRKO_PG_HOST</c> (with <c>BIRKO_PG_PORT</c>, <c>BIRKO_PG_USER</c>,
/// <c>BIRKO_PG_PASSWORD</c>) or <c>BIRKO_MONGO_HOST</c> (with <c>BIRKO_MONGO_PORT</c>) is set, are skipped otherwise,
/// and fail instead of skipping under <c>BIRKO_REQUIRE_LIVE</c>. The read side uses a second factory on the same database,
/// as a restarted server would.
/// </summary>
public class EveryStoreOnEveryDatabaseTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_everydb_{Guid.NewGuid():N}");
    private readonly List<Func<Task>> _cleanup = new();

    public void Dispose()
    {
        foreach (var clean in _cleanup)
            try { clean().GetAwaiter().GetResult(); } catch (Exception ex) { output.WriteLine($"cleanup: {ex.Message}"); }
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    public static TheoryData<string> Databases => new() { "SqLite", "InMemory", "PostgreSQL", "MongoDB" };

    private static string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

    /// <summary>The write and read factories for <paramref name="backend"/>, or null when its live database is not configured.</summary>
    private async Task<(IStoreFactory Write, IStoreFactory Read)?> FactoriesAsync(string backend)
    {
        switch (backend)
        {
            case "SqLite":
                Directory.CreateDirectory(_dir);
                var file = Path.Combine(_dir, "koboldlair.db");
                return (DataStores.SqLite(file), DataStores.SqLite(file));
            case "InMemory":
                var memory = DataStores.Create("InMemory", ""); // one process's memory: nothing to restart into
                return (memory, memory);
            case "PostgreSQL":
            {
                if (Live("BIRKO_PG_HOST") is not { } host) return null;
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = host, Port = int.Parse(Env("BIRKO_PG_PORT") ?? "5432"),
                    Username = Env("BIRKO_PG_USER") ?? "postgres", Password = Env("BIRKO_PG_PASSWORD") ?? "postgres",
                    Database = "postgres"
                };
                var admin = builder.ConnectionString;
                var database = $"dracode_live_{Guid.NewGuid():N}";
                await ExecuteAsync(admin, $"CREATE DATABASE \"{database}\"");
                _cleanup.Add(async () =>
                {
                    NpgsqlConnection.ClearAllPools();
                    await ExecuteAsync(admin, $"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)");
                });
                builder.Database = database;
                return (DataStores.Create("PostgreSQL", builder.ConnectionString), DataStores.Create("PostgreSQL", builder.ConnectionString));
            }
            case "MongoDB":
            {
                if (Live("BIRKO_MONGO_HOST") is not { } host) return null;
                var server = $"mongodb://{host}:{Env("BIRKO_MONGO_PORT") ?? "27017"}";
                var database = $"dracode_live_{Guid.NewGuid():N}";
                _cleanup.Add(() => new MongoClient(server).DropDatabaseAsync(database));
                return (DataStores.Create("MongoDB", $"{server}/{database}"), DataStores.Create("MongoDB", $"{server}/{database}"));
            }
            default:
                throw new ArgumentOutOfRangeException(nameof(backend));
        }
    }

    private string? Live(string variable)
    {
        if (Env(variable) is { } value) return value;
        if (Env("BIRKO_REQUIRE_LIVE") != null)
            throw new InvalidOperationException($"{variable} is not set but BIRKO_REQUIRE_LIVE is.");
        output.WriteLine($"SKIPPED: {variable} not set.");
        return null;
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [Theory]
    [MemberData(nameof(Databases))]
    public async Task Every_store_writes_and_reads_back(string backend)
    {
        if (await FactoriesAsync(backend) is not var (write, read)) return;
        var now = new DateTime(2026, 11, 28, 12, 0, 0, DateTimeKind.Utc);

        // Write everything through one factory ...
        var project = new Project { Name = "everydb", OwnerId = "owner-1" };
        var projects = new SqlProjectRepository(write);
        await projects.InitializeAsync();
        await projects.AddAsync(project);

        var task = new TaskRecord { Id = Guid.NewGuid().ToString(), Task = "build the menu", Priority = TaskPriority.High };
        var tasks = new SqlTaskRepository(write);
        await tasks.InitializeAsync();
        await tasks.AddTaskAsync(project.Id, "backend", task);

        var decision = new PendingDecision { ProjectId = project.Id, TaskId = task.Id, Source = PendingDecisionSource.AskHuman, Question = "{\"text\":\"Which?\"}" };
        var decisions = new SqlPendingDecisionRepository(write);
        await decisions.InitializeAsync();
        await decisions.CreateAsync(decision);

        var plans = new SqlPlanRepository(write);
        await plans.InitializeAsync();
        await plans.SavePlanAsync(new KoboldImplementationPlan { ProjectId = project.Id, TaskId = task.Id, TaskDescription = "build the menu" });

        var history = new SqlHistoryRepository(write);
        await history.InitializeAsync();
        await history.SaveHistoryAsync("everydb", new List<string> { "hello dragon" });

        var layouts = new SqlLayoutRepository(write);
        await layouts.InitializeAsync();
        await layouts.SaveCurrentAsync("owner-1", "{\"openTab\":\"board\"}");

        var budgets = new SqlBudgetRepository(write);
        await budgets.InitializeAsync();
        await budgets.SaveAsync(50, 500, 100, 80, "owner-1");

        var usage = new SqlUsageRepository(write, NullLogger.Instance);
        await usage.InitializeAsync();
        await usage.RecordUsageAsync(new UsageRecordEntity { Provider = "lan2", Model = "m", TotalTokens = 10, EstimatedCostUsd = 0.5, ProjectId = project.Id, RecordedAt = now });

        var providers = new SqlProviderConfigRepository(write);
        await providers.InitializeAsync();
        await providers.UpsertProviderAsync(new ProviderConfigEntity { Name = "lan2", Type = "vllm", BaseUrl = "http://lan2:8000" });

        var users = new SqlUserRepository(write);
        await users.InitializeAsync();
        await users.UpsertAsync(new User { Sub = "owner-1", DisplayName = "Owner" });

        await new ServiceKeyRegistry(write).AddAsync(new ServiceKey("client-1", "owner-1", "ci", now));
        await new SpecificationEventService(write.EventStore()).RecordSpecificationCreatedAsync("spec-1", "everydb", "# Menu", "everydb");

        // ... and read it all back through another, as a restarted server would
        var projects2 = new SqlProjectRepository(read);
        await projects2.InitializeAsync();
        projects2.GetById(project.Id)!.OwnerId.Should().Be("owner-1", "projects");

        var tasks2 = new SqlTaskRepository(read);
        await tasks2.InitializeAsync();
        (await tasks2.GetByIdAsync(task.Id))!.Priority.Should().Be(TaskPriority.High, "tasks");

        var decisions2 = new SqlPendingDecisionRepository(read);
        await decisions2.InitializeAsync();
        (await decisions2.GetUnresolvedForTaskAsync(task.Id))!.Id.Should().Be(decision.Id, "decisions");

        var plans2 = new SqlPlanRepository(read);
        await plans2.InitializeAsync();
        (await plans2.LoadPlanAsync(project.Id, task.Id))!.TaskDescription.Should().Be("build the menu", "plans");

        var history2 = new SqlHistoryRepository(read);
        await history2.InitializeAsync();
        (await history2.LoadHistoryJsonAsync("everydb")).Should().Contain("hello dragon", "Dragon history");

        var layouts2 = new SqlLayoutRepository(read);
        await layouts2.InitializeAsync();
        (await layouts2.GetCurrentAsync("owner-1")).Should().Contain("board", "layouts");

        var budgets2 = new SqlBudgetRepository(read);
        await budgets2.InitializeAsync();
        (await budgets2.GetAsync())!.MonthlyBudgetUsd.Should().Be(500, "budgets");

        var usage2 = new SqlUsageRepository(read, NullLogger.Instance);
        await usage2.InitializeAsync();
        (await usage2.GetProjectSpendAsync(project.Id)).Should().BeApproximately(0.5, 1e-9, "usage");
        (await usage2.GetUsageByProviderAsync(now.AddHours(-1), now.AddHours(1)))
            .Should().ContainSingle(r => r.Provider == "lan2" && r.TotalTokens == 10, "the cost report's aggregation");

        var providers2 = new SqlProviderConfigRepository(read);
        await providers2.InitializeAsync();
        (await providers2.GetProviderAsync("lan2"))!.BaseUrl.Should().Be("http://lan2:8000", "providers");

        var users2 = new SqlUserRepository(read);
        await users2.InitializeAsync();
        (await users2.GetBySubAsync("owner-1"))!.DisplayName.Should().Be("Owner", "users");

        (await new ServiceKeyRegistry(read).GetAsync("client-1"))!.OwnerId.Should().Be("owner-1", "service keys");
        (await new SpecificationEventService(read.EventStore()).GetSpecificationHistoryAsync("spec-1"))
            .Should().ContainSingle("specification history");
        output.WriteLine($"{backend}: every store wrote and read back");
    }
}
