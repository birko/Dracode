using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// The acceptance check counts its reopens on the task (TASK-025, FEATURE-031 D3). Every existing database has a
/// <c>tasks</c> table without that column, and <c>CREATE TABLE IF NOT EXISTS</c> never adds one, so the repository's
/// initialization does — or every task write would fail on such a database.
/// </summary>
public class TaskAcceptanceAttemptsColumnTests : IAsyncLifetime
{
    private string _dbPath = null!;

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_taskcol_{Guid.NewGuid():N}.db");
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

    [Fact]
    public async Task A_tasks_table_from_before_the_counter_gains_it_and_keeps_the_count()
    {
        await new SqlTaskRepository(_dbPath).InitializeAsync();
        SqliteConnection.ClearAllPools();
        Execute("ALTER TABLE \"tasks\" DROP COLUMN \"AcceptanceAttempts\";"); // the table as an older database holds it

        var repo = new SqlTaskRepository(_dbPath);
        await repo.InitializeAsync();
        var task = new TaskRecord { Task = "[api-1] Build the thing", AcceptanceAttempts = 2 };
        await repo.AddTaskAsync("proj-1", "backend", task);

        (await repo.GetByIdAsync(task.Id))!.AcceptanceAttempts.Should().Be(2);
    }
}
