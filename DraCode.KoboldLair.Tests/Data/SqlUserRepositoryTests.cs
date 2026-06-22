using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Users;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// Integration tests for SqlUserRepository using a real SQLite database.
/// Each test gets a fresh temp database file. (TASK-034 / FEATURE-019)
/// </summary>
public class SqlUserRepositoryTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private SqlUserRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_user_test_{Guid.NewGuid():N}.db");
        _repo = new SqlUserRepository(_dbPath);
        await _repo.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetBySubAsync_ShouldReturnNull_WhenAbsent()
    {
        (await _repo.GetBySubAsync("nobody")).Should().BeNull();
    }

    [Fact]
    public async Task UpsertAsync_ShouldInsertThenRoundTripBySub()
    {
        await _repo.UpsertAsync(new User
        {
            Sub = "github|123",
            DisplayName = "Ada",
            Email = "ada@example.com"
        });

        var loaded = await _repo.GetBySubAsync("github|123");
        loaded.Should().NotBeNull();
        loaded!.DisplayName.Should().Be("Ada");
        loaded.Email.Should().Be("ada@example.com");
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdateInPlace_NotDuplicate()
    {
        await _repo.UpsertAsync(new User { Sub = "github|123", DisplayName = "Ada" });
        await _repo.UpsertAsync(new User { Sub = "github|123", DisplayName = "Ada Lovelace", Email = "ada@example.com" });

        var loaded = await _repo.GetBySubAsync("github|123");
        loaded!.DisplayName.Should().Be("Ada Lovelace");
        loaded.Email.Should().Be("ada@example.com");
    }

    [Fact]
    public async Task UpsertAsync_ShouldReject_EmptySub()
    {
        var act = async () => await _repo.UpsertAsync(new User { Sub = "" });
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task UpsertAsync_ShouldPersistAcrossReload()
    {
        await _repo.UpsertAsync(new User { Sub = "github|123", DisplayName = "Ada" });

        var reopened = new SqlUserRepository(_dbPath);
        await reopened.InitializeAsync();

        (await reopened.GetBySubAsync("github|123"))!.DisplayName.Should().Be("Ada");
    }
}
