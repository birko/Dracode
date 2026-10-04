using DraCode.KoboldLair.Data.Repositories;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// A spec path stored with mixed separators (a forward-slash ProjectsPath combined with Path.Combine) must still be
/// found by its normalised form, or approved features never send the project back to Wyvern (TASK-097 / FIELD-012).
/// </summary>
public class SpecificationPathLookupTests : IAsyncLifetime
{
    private string _dir = null!;
    private string _forwardSlashDir = null!;

    public Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_specpath_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _forwardSlashDir = _dir.Replace('\\', '/');
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    // What a forward-slash ProjectsPath produces on Windows, e.g. C:/Source/DraCode-Projects\p\specification.md
    private string MixedSpecPath(string name) => Path.Combine(_forwardSlashDir, name, "specification.md");

    private static string BackslashForm(string path) => path.Replace('/', '\\');

    private async Task<SqlProjectRepository> SqlRepo()
    {
        var repo = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await repo.InitializeAsync();
        return repo;
    }

    private static Project NewProject(string name, string specPath) => new()
    {
        Name = name,
        OwnerId = Guid.NewGuid().ToString(),
        Status = ProjectStatus.Analyzed,
        Paths = { Specification = specPath }
    };

    public static IEnumerable<object[]> Repositories() => new[] { new object[] { "sql" }, new object[] { "json" } };

    private async Task<IProjectRepository> Repo(string kind) =>
        kind == "sql" ? await SqlRepo() : new ProjectRepository(_dir);

    [Theory]
    [MemberData(nameof(Repositories))]
    public async Task A_mixed_separator_spec_path_is_found_by_its_backslash_and_forward_slash_forms(string kind)
    {
        var repo = await Repo(kind);
        var project = NewProject("p", MixedSpecPath("p"));
        await repo.AddAsync(project);

        repo.GetBySpecificationPath(BackslashForm(project.Paths.Specification)).Should().NotBeNull()
            .And.Subject.As<Project>().Id.Should().Be(project.Id);
        repo.GetBySpecificationPath(project.Paths.Specification.Replace('\\', '/')).Should().NotBeNull()
            .And.Subject.As<Project>().Id.Should().Be(project.Id);
    }

    [Fact]
    public async Task MarkSpecificationModified_sends_an_analyzed_project_with_a_mixed_path_back_to_Wyvern()
    {
        var repo = await SqlRepo();
        var project = NewProject("q", MixedSpecPath("q"));
        await repo.AddAsync(project);

        var config = new KoboldLairConfiguration { ProjectsPath = _forwardSlashDir };
        var providerConfig = new ProviderConfigurationService(
            NullLogger<ProviderConfigurationService>.Instance, Options.Create(config), Path.Combine(_dir, "user-settings.json"));
        var service = new ProjectService(repo, new WyvernFactory(providerConfig, repo, config),
            NullLogger<ProjectService>.Instance, new GitService(NullLogger<GitService>.Instance), config);

        service.MarkSpecificationModified(BackslashForm(project.Paths.Specification));

        repo.GetById(project.Id)!.Status.Should().Be(ProjectStatus.SpecificationModified);
    }
}
