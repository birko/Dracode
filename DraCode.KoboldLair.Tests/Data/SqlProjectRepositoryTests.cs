using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// Integration tests for SqlProjectRepository using real SQLite database.
/// Each test gets a fresh temp database file.
/// </summary>
public class SqlProjectRepositoryTests : IAsyncLifetime
{
    private string _dbPath = null!;
    private SqlProjectRepository _repo = null!;

    public async Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_test_{Guid.NewGuid():N}.db");
        _repo = new SqlProjectRepository(_dbPath);
        await _repo.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        try { File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    private static Project CreateTestProject(string name = "test-project")
    {
        return new Project
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Status = ProjectStatus.New,
            ExecutionState = ProjectExecutionState.Running,
            Paths = new ProjectPaths
            {
                Specification = $"./{name}/specification.md",
                Output = $"./{name}/workspace"
            },
            Agents = new AgentsConfig
            {
                Wyrm = new AgentConfig { Enabled = true, MaxParallel = 1 },
                Wyvern = new AgentConfig { Enabled = true, MaxParallel = 1 },
                Drake = new AgentConfig { Enabled = true, MaxParallel = 1 },
                KoboldPlanner = new AgentConfig { Enabled = true, MaxParallel = 1 },
                Kobold = new AgentConfig { Enabled = true, Provider = "openai", MaxParallel = 4 }
            },
            Security = new SecurityConfig { SandboxMode = "workspace" }
        };
    }

    [Fact]
    public async Task AddAsync_ShouldPersistProject()
    {
        var project = CreateTestProject();

        await _repo.AddAsync(project);

        var loaded = _repo.GetById(project.Id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("test-project");
        loaded.Status.Should().Be(ProjectStatus.New);
    }

    [Fact]
    public async Task UpdateAsync_ShouldModifyProject()
    {
        var project = CreateTestProject();
        await _repo.AddAsync(project);

        project.Status = ProjectStatus.Analyzed;
        project.Name = "updated-name";
        await _repo.UpdateAsync(project);

        var loaded = _repo.GetById(project.Id);
        loaded!.Status.Should().Be(ProjectStatus.Analyzed);
        loaded.Name.Should().Be("updated-name");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveProject()
    {
        var project = CreateTestProject();
        await _repo.AddAsync(project);

        await _repo.DeleteAsync(project.Id);

        _repo.GetById(project.Id).Should().BeNull();
        _repo.Count().Should().Be(0);
    }

    [Fact]
    public async Task GetByName_ShouldFindCaseInsensitive()
    {
        var project = CreateTestProject("My-Project");
        await _repo.AddAsync(project);

        _repo.GetByName("my-project").Should().NotBeNull();
        _repo.GetByName("MY-PROJECT").Should().NotBeNull();
    }

    [Fact]
    public async Task GetByStatuses_ShouldFilterCorrectly()
    {
        var p1 = CreateTestProject("p1");
        p1.Status = ProjectStatus.New;
        var p2 = CreateTestProject("p2");
        p2.Status = ProjectStatus.Analyzed;
        var p3 = CreateTestProject("p3");
        p3.Status = ProjectStatus.Completed;

        await _repo.AddAsync(p1);
        await _repo.AddAsync(p2);
        await _repo.AddAsync(p3);

        var result = _repo.GetByStatuses(ProjectStatus.New, ProjectStatus.Analyzed);
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task NestedObjects_ShouldRoundTrip()
    {
        var project = CreateTestProject();
        project.Agents.Kobold.Provider = "claude";
        project.Agents.Kobold.Model = "claude-sonnet-4-20250514";
        project.Agents.Kobold.MaxParallel = 8;
        project.Security.AllowedExternalPaths.Add("C:\\Source\\MyApp");
        project.Security.SandboxMode = "relaxed";
        project.Metadata["key1"] = "value1";
        project.ExternalProjectReferences.Add(new ExternalProjectReference
        {
            Name = "ext-proj",
            Path = "C:\\Source\\ExtProj",
            RelativePath = "../ExtProj"
        });

        await _repo.AddAsync(project);

        // Force reload from DB
        var freshRepo = new SqlProjectRepository(_dbPath);
        await freshRepo.InitializeAsync();

        var loaded = freshRepo.GetById(project.Id);
        loaded.Should().NotBeNull();
        loaded!.Agents.Kobold.Provider.Should().Be("claude");
        loaded.Agents.Kobold.Model.Should().Be("claude-sonnet-4-20250514");
        loaded.Agents.Kobold.MaxParallel.Should().Be(8);
        loaded.Security.AllowedExternalPaths.Should().Contain("C:\\Source\\MyApp");
        loaded.Security.SandboxMode.Should().Be("relaxed");
        loaded.Metadata["key1"].Should().Be("value1");
        loaded.ExternalProjectReferences.Should().HaveCount(1);
        loaded.ExternalProjectReferences[0].Name.Should().Be("ext-proj");
    }

    [Fact]
    public async Task AgentConfig_ShouldWorkThroughInterface()
    {
        var project = CreateTestProject();
        await _repo.AddAsync(project);

        await _repo.SetAgentLimitAsync(project.Id, "kobold", 6);
        await _repo.SetProjectProviderAsync(project.Id, "kobold", "gemini", "gemini-2.5-pro");

        _repo.GetMaxParallel(project.Id, "kobold").Should().Be(6);
        _repo.GetProjectProvider(project.Id, "kobold").Should().Be("gemini");
        _repo.GetProjectModel(project.Id, "kobold").Should().Be("gemini-2.5-pro");
    }

    [Fact]
    public async Task Count_ShouldTrack()
    {
        _repo.Count().Should().Be(0);

        await _repo.AddAsync(CreateTestProject("a"));
        await _repo.AddAsync(CreateTestProject("b"));
        _repo.Count().Should().Be(2);

        await _repo.DeleteAsync(_repo.GetByName("a")!.Id);
        _repo.Count().Should().Be(1);
    }

    [Fact]
    public async Task ConcurrentAdds_ShouldNotCorrupt()
    {
        var tasks = Enumerable.Range(0, 20)
            .Select(i => _repo.AddAsync(CreateTestProject($"concurrent-{i}")));

        await Task.WhenAll(tasks);

        _repo.Count().Should().Be(20);
        _repo.GetAll().Select(p => p.Name).Distinct().Should().HaveCount(20);
    }
}
