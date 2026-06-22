using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Tests the project-ownership guard on ProjectService create paths (TASK-034 / FEATURE-019 D8):
/// every create requires a real owner `sub`.
/// </summary>
public class ProjectServiceOwnershipTests : IAsyncLifetime
{
    private string _dir = null!;
    private string _dbPath = null!;
    private SqlProjectRepository _repo = null!;
    private ProjectService _service = null!;

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_svc_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "koboldlair.db");

        _repo = new SqlProjectRepository(_dbPath);
        await _repo.InitializeAsync();

        var config = new KoboldLairConfiguration { ProjectsPath = _dir };
        var providerConfig = new ProviderConfigurationService(
            NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(config),
            Path.Combine(_dir, "user-settings.json"));
        var projectConfig = new ProjectConfigurationService(
            providerConfig,
            NullLogger<ProjectConfigurationService>.Instance,
            Path.Combine(_dir, "project-configs.json"));
        var wyvernFactory = new WyvernFactory(providerConfig, projectConfig, config);
        var gitService = new GitService(NullLogger<GitService>.Instance);

        _service = new ProjectService(_repo, wyvernFactory, NullLogger<ProjectService>.Instance, gitService, config);
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterProject_ShouldReject_MissingOwner(string owner)
    {
        var act = () => _service.RegisterProject("p", Path.Combine(_dir, "p", "specification.md"), owner);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RegisterExistingProject_ShouldReject_MissingOwner(string owner)
    {
        var act = () => _service.RegisterExistingProject("p", _dir, owner);
        act.Should().Throw<ArgumentException>();
    }
}
