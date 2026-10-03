using DraCode.KoboldLair.Data.Entities;
using DraCode.KoboldLair.Data.Repositories.Sql;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// Integration tests for <see cref="SqlUsageRepository"/> against a real SQLite file (TASK-082): a
/// <c>usage_records</c> table created before Birko mapped <c>double</c> to a column has no
/// <c>EstimatedCostUsd</c>, and <c>CREATE TABLE IF NOT EXISTS</c> never adds it — so initialization must.
/// <see cref="SqlUsageRepository.RecordUsageAsync(UsageRecordEntity)"/> swallows its exceptions, which
/// is how this went unnoticed, so every assertion reads the data back rather than relying on a throw.
/// </summary>
public class SqlUsageRepositoryTests : IAsyncLifetime
{
    // The table exactly as a pre-August-2026 database holds it (no EstimatedCostUsd), with one row.
    private const string OldShapeDdl =
        "CREATE TABLE IF NOT EXISTS \"usage_records\" (Provider TEXT NOT NULL, Model TEXT, PromptTokens INTEGER NOT NULL, " +
        "CompletionTokens INTEGER NOT NULL, TotalTokens INTEGER NOT NULL, ProjectId TEXT, TaskId TEXT, AgentType TEXT, " +
        "CallerContext TEXT, RecordedAt INTEGER NOT NULL, Guid TEXT PRIMARY KEY UNIQUE, CreatedAt INTEGER NOT NULL, " +
        "UpdatedAt INTEGER NOT NULL, PrevUpdatedAt INTEGER);";

    private const string OldRowInsert =
        "INSERT INTO \"usage_records\" (Provider, Model, PromptTokens, CompletionTokens, TotalTokens, RecordedAt, Guid, CreatedAt, UpdatedAt) " +
        "VALUES ('old-provider', '', 10, 5, 15, '2026-06-25 04:40:27.7299547', '948a6e49-3423-4462-a8a0-e73cff4d6ff4', " +
        "'2026-06-25 04:40:27.7299938', '2026-06-25 04:40:27.7300255');";

    private string _dbPath = null!;

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_usage_{Guid.NewGuid():N}.db");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    private void Execute(string sql)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private object? Scalar(string sql)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath};Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    private async Task<SqlUsageRepository> InitializedRepoAsync()
    {
        var repo = new SqlUsageRepository(_dbPath, NullLogger.Instance);
        await repo.InitializeAsync();
        return repo;
    }

    private static UsageRecordEntity Usage(double cost) => new()
    {
        Provider = "new-provider",
        Model = "m",
        PromptTokens = 100,
        CompletionTokens = 20,
        TotalTokens = 120,
        EstimatedCostUsd = cost,
        RecordedAt = DateTime.UtcNow
    };

    [Fact]
    public async Task Old_shape_table_gains_the_cost_column_and_a_usage_record_then_persists()
    {
        Execute(OldShapeDdl);
        Execute(OldRowInsert);

        var repo = await InitializedRepoAsync();
        await repo.RecordUsageAsync(Usage(0.42));

        Scalar("SELECT COUNT(*) FROM usage_records").Should().Be(2L, "the old row is kept and the new one is saved");
        (await repo.GetTotalSpendAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1)))
            .Should().BeApproximately(0.42, 1e-9);
        Scalar("SELECT EstimatedCostUsd FROM usage_records WHERE Provider = 'old-provider'")
            .Should().Be(0.0, "rows written before the column existed read back as zero cost");
    }

    [Fact]
    public async Task Initializing_an_upgraded_table_again_changes_nothing()
    {
        Execute(OldShapeDdl);
        Execute(OldRowInsert);

        await InitializedRepoAsync();
        var repo = await InitializedRepoAsync();
        await repo.RecordUsageAsync(Usage(1.5));

        Scalar("SELECT COUNT(*) FROM pragma_table_info('usage_records') WHERE name = 'EstimatedCostUsd'").Should().Be(1L);
        Scalar("SELECT COUNT(*) FROM usage_records").Should().Be(2L);
    }

    [Fact]
    public async Task Fresh_database_records_and_reads_back_cost()
    {
        var repo = await InitializedRepoAsync();
        await repo.RecordUsageAsync(Usage(0.25));
        await repo.RecordUsageAsync(Usage(0.75));

        (await repo.GetTotalSpendAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1)))
            .Should().BeApproximately(1.0, 1e-9);
    }
}
