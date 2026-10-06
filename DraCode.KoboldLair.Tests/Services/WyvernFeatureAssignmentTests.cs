using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Wyvern must be given the project's features: Ready ones go into the prompt and are marked AssignedToWyvern once the
/// analysis succeeds, persisted to the features sidecar (TASK-099 / FIELD-014).
/// </summary>
public class WyvernFeatureAssignmentTests : IDisposable
{
    // The task carries the feature's id: since TASK-102 only a feature that received a task is assigned
    private const string ReplyWithTasks =
        "{\"projectName\":\"p\",\"areas\":[{\"name\":\"cli\",\"tasks\":[{\"id\":\"cli-1\",\"name\":\"greet\",\"description\":\"Write greet.py\",\"agentType\":\"python\",\"featureId\":\"f1\"}]}]}";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_wyvern_features_test_{Guid.NewGuid():N}");
    private readonly string _specPath;

    public WyvernFeatureAssignmentTests()
    {
        Directory.CreateDirectory(_dir);
        _specPath = Path.Combine(_dir, SpecificationService.SpecFileName);
        File.WriteAllText(_specPath, "# p\n\nA script greet.py that prints Hello.");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private sealed class RecordingProvider(string reply) : ILlmProvider
    {
        public List<string> Prompts { get; } = new();
        public string Name => "canned";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            Prompts.Add(string.Join("\n", messages.Select(m => m.Content?.ToString() ?? string.Empty)));
            return Task.FromResult(new LlmResponse
            {
                StopReason = "end_turn",
                Content = new List<ContentBlock> { new() { Type = "text", Text = reply } }
            });
        }

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private async Task SaveFeatureSidecarAsync()
    {
        var spec = new Specification { Name = "p", FilePath = _specPath, ProjectFolder = _dir };
        spec.Features.Add(new Feature { Id = "f1", Name = "Greeting script", Description = "greet.py prints Hello", Status = FeatureStatus.Ready });
        await SpecificationService.PersistFeaturesAsync(spec);
    }

    private Wyvern NewWyvern(ILlmProvider provider)
    {
        var options = new AgentOptions { WorkingDirectory = _dir, Verbose = false };
        return new Wyvern("p", _specPath, new WyvernAgent(provider, options), "canned",
            new Dictionary<string, string>(), options, _dir);
    }

    private async Task<FeatureStatus> StoredStatusAsync()
    {
        var reloaded = new Specification();
        await SpecificationService.LoadFeaturesAsync(reloaded, _dir);
        return reloaded.Features.Single().Status;
    }

    [Fact]
    public async Task The_specification_for_analysis_carries_the_projects_features()
    {
        await SaveFeatureSidecarAsync();
        var project = new Project { Name = "p", Paths = { Specification = _specPath } };

        var spec = await ProjectService.LoadSpecificationForAnalysisAsync(project);

        spec.Should().NotBeNull();
        spec!.Features.Should().ContainSingle(f => f.Id == "f1" && f.Status == FeatureStatus.Ready);
    }

    [Fact]
    public async Task A_successful_analysis_puts_Ready_features_in_the_prompt_and_persists_them_as_assigned()
    {
        await SaveFeatureSidecarAsync();
        var spec = await ProjectService.LoadSpecificationForAnalysisAsync(new Project { Name = "p", Paths = { Specification = _specPath } });
        var provider = new RecordingProvider(ReplyWithTasks);

        var analysis = await NewWyvern(provider).AnalyzeProjectAsync(spec);

        provider.Prompts.First().Should().Contain("Greeting script");
        (await StoredStatusAsync()).Should().Be(FeatureStatus.AssignedToWyvern);
        analysis.ProcessedFeatures.Should().Contain("f1");
    }

    [Fact]
    public async Task A_failed_analysis_leaves_features_Ready_for_the_next_attempt()
    {
        await SaveFeatureSidecarAsync();
        var spec = await ProjectService.LoadSpecificationForAnalysisAsync(new Project { Name = "p", Paths = { Specification = _specPath } });

        var analyze = () => NewWyvern(new RecordingProvider("{}")).AnalyzeProjectAsync(spec);

        await analyze.Should().ThrowAsync<InvalidOperationException>();
        (await StoredStatusAsync()).Should().Be(FeatureStatus.Ready);
    }
}
