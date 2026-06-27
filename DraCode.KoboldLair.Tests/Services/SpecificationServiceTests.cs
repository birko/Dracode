using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Round-trip coverage for <see cref="SpecificationService"/> (TASK-043 step 1) — the canonical
/// spec/feature persistence extracted from the duplicated Dragon-tool logic. Verifies the on-disk
/// layout (<c>specification.md</c> + wrapped <c>specification.features.json</c>), version/hash bumping,
/// the legacy bare-array features fallback, and name→folder resolution.
/// </summary>
public class SpecificationServiceTests : IDisposable
{
    private readonly string _root;
    private readonly SpecificationService _svc;

    public SpecificationServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "koboldlair-spec-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _svc = new SpecificationService(_root);
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void SanitizeProjectName_lowercases_and_dashes_spaces()
    {
        SpecificationService.SanitizeProjectName("My Todo App").Should().Be("my-todo-app");
    }

    [Fact]
    public async Task LoadByNameAsync_returns_null_when_no_spec_exists()
    {
        (await _svc.LoadByNameAsync("nope")).Should().BeNull();
    }

    [Fact]
    public async Task SaveContentAsync_create_then_load_round_trips_content()
    {
        var saved = await _svc.SaveContentAsync("Demo", "# Demo\n\nbody");

        saved.Version.Should().Be(1);
        saved.ContentHash.Should().Be(Specification.ComputeHash("# Demo\n\nbody"));

        var loaded = await _svc.LoadByNameAsync("Demo");
        loaded.Should().NotBeNull();
        loaded!.Content.Should().Be("# Demo\n\nbody");
        loaded.ProjectFolder.Should().Be(_svc.ResolveProjectFolder("Demo"));
        File.Exists(Path.Combine(_svc.ResolveProjectFolder("Demo"), SpecificationService.SpecFileName))
            .Should().BeTrue();
    }

    [Fact]
    public async Task SaveContentAsync_update_bumps_version_and_hash()
    {
        await _svc.SaveContentAsync("Demo", "v1 body");
        var updated = await _svc.SaveContentAsync("Demo", "v2 body");

        updated.Version.Should().Be(2);
        updated.ContentHash.Should().Be(Specification.ComputeHash("v2 body"));

        (await _svc.LoadByNameAsync("Demo"))!.Content.Should().Be("v2 body");
    }

    [Fact]
    public async Task PersistFeatures_then_Load_round_trips_features_and_version_metadata()
    {
        var folder = _svc.ResolveProjectFolder("Demo");
        Directory.CreateDirectory(folder);
        var spec = new Specification
        {
            Name = "Demo",
            ProjectFolder = folder,
            Version = 3,
            ContentHash = "abc123",
            Features = { new Feature { Name = "Login", Description = "auth", Priority = "high", Status = FeatureStatus.Draft } }
        };

        await SpecificationService.PersistFeaturesAsync(spec);

        var reloaded = new Specification { Name = "Demo", ProjectFolder = folder };
        await SpecificationService.LoadFeaturesAsync(reloaded, folder);

        reloaded.Features.Should().ContainSingle().Which.Name.Should().Be("Login");
        reloaded.Version.Should().Be(3);
        reloaded.ContentHash.Should().Be("abc123");
    }

    [Fact]
    public async Task LoadFeatures_reads_legacy_bare_array_format()
    {
        var folder = _svc.ResolveProjectFolder("Legacy");
        Directory.CreateDirectory(folder);
        var legacyJson = """[{"Name":"Old","Description":"d","Priority":"medium","Status":0}]""";
        await File.WriteAllTextAsync(Path.Combine(folder, SpecificationService.FeaturesFileName), legacyJson);

        var spec = new Specification { Name = "Legacy", ProjectFolder = folder };
        await SpecificationService.LoadFeaturesAsync(spec, folder);

        spec.Features.Should().ContainSingle().Which.Name.Should().Be("Old");
    }

    [Fact]
    public async Task LoadFeatures_is_silent_when_sidecar_absent()
    {
        var folder = _svc.ResolveProjectFolder("Empty");
        Directory.CreateDirectory(folder);

        var spec = new Specification { Name = "Empty", ProjectFolder = folder };
        var act = async () => await SpecificationService.LoadFeaturesAsync(spec, folder);

        await act.Should().NotThrowAsync();
        spec.Features.Should().BeEmpty();
    }
}
