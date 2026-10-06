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
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Wyvern links each task to its feature and re-analysis adds only new work (TASK-102 / FIELD-016): the prompt names
/// features by id and lists existing tasks, a reply's featureId is carried onto the task record and the feature's task
/// list, and a re-analysis skips tasks that already exist (by id in any area, or by name when already Done).
/// </summary>
public class WyvernFeatureLinkingTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_wyvern_linking_test_{Guid.NewGuid():N}");
    private readonly string _specPath;

    public WyvernFeatureLinkingTests()
    {
        Directory.CreateDirectory(_dir);
        _specPath = Path.Combine(_dir, SpecificationService.SpecFileName);
        File.WriteAllText(_specPath, "# p\n\nA script greet.py that prints Hello.");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private sealed class SequenceProvider(params string[] replies) : ILlmProvider
    {
        private int _next;
        public List<string> Prompts { get; } = new();
        public string Name => "canned";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            Prompts.Add(string.Join("\n", messages.Select(m => m.Content?.ToString() ?? string.Empty)));
            var reply = replies[Math.Min(_next++, replies.Length - 1)];
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

    private static string Reply(params (string Area, string Id, string Name, string? FeatureId)[] tasks)
    {
        var areas = tasks.GroupBy(t => t.Area).Select(g =>
            "{\"name\":\"" + g.Key + "\",\"tasks\":[" + string.Join(",", g.Select(t =>
                "{\"id\":\"" + t.Id + "\",\"name\":\"" + t.Name + "\",\"description\":\"" + t.Name + "\",\"agentType\":\"python\"" +
                (t.FeatureId == null ? "" : ",\"featureId\":\"" + t.FeatureId + "\"") + "}")) + "]}");
        return "{\"projectName\":\"p\",\"areas\":[" + string.Join(",", areas) + "]}";
    }

    private async Task<Specification> SpecWithFeaturesAsync(params (string Id, string Name, FeatureStatus Status)[] features)
    {
        var spec = new Specification { Name = "p", FilePath = _specPath, ProjectFolder = _dir };
        foreach (var (id, name, status) in features)
            spec.Features.Add(new Feature { Id = id, Name = name, Description = name, Status = status });
        await SpecificationService.PersistFeaturesAsync(spec);
        return (await ProjectService.LoadSpecificationForAnalysisAsync(new Project { Name = "p", Paths = { Specification = _specPath } }))!;
    }

    private Wyvern NewWyvern(ILlmProvider provider)
    {
        var options = new AgentOptions { WorkingDirectory = _dir, Verbose = false };
        return new Wyvern("p", _specPath, new WyvernAgent(provider, options), "canned",
            new Dictionary<string, string>(), options, _dir);
    }

    private List<TaskRecord> AllTaskRecords()
    {
        var tasksDir = Path.Combine(_dir, "tasks");
        return Directory.GetFiles(tasksDir, "*-tasks.md").SelectMany(f =>
        {
            var tracker = new TaskTracker();
            tracker.LoadFromFile(f);
            return tracker.GetAllTasks();
        }).ToList();
    }

    private async Task<List<Feature>> StoredFeaturesAsync()
    {
        var reloaded = new Specification();
        await SpecificationService.LoadFeaturesAsync(reloaded, _dir);
        return reloaded.Features;
    }

    [Fact]
    public async Task A_replys_featureId_is_carried_to_the_task_record_and_the_features_task_list()
    {
        var spec = await SpecWithFeaturesAsync(("f1", "Greeting script", FeatureStatus.Ready));
        var provider = new SequenceProvider(Reply(("cli", "cli-1", "Implement greet.py", "f1")));
        var wyvern = NewWyvern(provider);

        await wyvern.AnalyzeProjectAsync(spec);
        await wyvern.CreateTasksAsync();

        provider.Prompts.First().Should().Contain("id: f1");
        AllTaskRecords().Should().ContainSingle().Which.FeatureId.Should().Be("f1");
        (await StoredFeaturesAsync()).Single().TaskIds.Should().Equal("cli-1");
    }

    [Fact]
    public async Task A_feature_named_instead_of_identified_still_links_and_an_unknown_id_does_not()
    {
        var spec = await SpecWithFeaturesAsync(("f1", "Greeting script", FeatureStatus.Ready));
        var wyvern = NewWyvern(new SequenceProvider(Reply(
            ("cli", "cli-1", "Implement greet.py", "greeting script"),
            ("cli", "cli-2", "Something else", "no-such-feature"))));

        var analysis = await wyvern.AnalyzeProjectAsync(spec);

        var tasks = analysis.Areas.SelectMany(a => a.Tasks).ToDictionary(t => t.Id);
        tasks["cli-1"].FeatureId.Should().Be("f1");
        tasks["cli-2"].FeatureId.Should().BeNull();
    }

    [Fact]
    public async Task A_feature_that_got_no_task_stays_Ready_for_the_next_analysis()
    {
        var spec = await SpecWithFeaturesAsync(("f1", "Greeting script", FeatureStatus.Ready), ("f2", "Uppercase flag", FeatureStatus.Ready));
        var wyvern = NewWyvern(new SequenceProvider(Reply(("cli", "cli-1", "Implement greet.py", "f1"))));

        await wyvern.AnalyzeProjectAsync(spec);

        var stored = (await StoredFeaturesAsync()).ToDictionary(f => f.Id);
        stored["f1"].Status.Should().Be(FeatureStatus.AssignedToWyvern);
        stored["f2"].Status.Should().Be(FeatureStatus.Ready);
    }

    [Fact]
    public async Task A_reanalysis_lists_existing_tasks_and_adds_only_the_new_features_work()
    {
        // First analysis: greet.py + README
        var first = NewWyvern(new SequenceProvider(Reply(
            ("cli", "cli-1", "Implement greet.py", "f1"),
            ("documentation", "doc-1", "Write README.md usage documentation", null))));
        await first.AnalyzeProjectAsync(await SpecWithFeaturesAsync(("f1", "Greeting script", FeatureStatus.Ready)));
        var (firstFiles, _) = await first.CreateTasksAsync();

        // Both tasks finished
        foreach (var file in firstFiles.Values)
        {
            var tracker = new TaskTracker();
            tracker.LoadFromFile(file);
            foreach (var record in tracker.GetAllTasks())
                tracker.UpdateTask(record, TaskStatus.Done);
            tracker.SaveToFile(file);
        }

        // A feature is added; the model re-emits cli-1 and a renamed README task alongside the new work
        var spec = await SpecWithFeaturesAsync(("f1", "Greeting script", FeatureStatus.AssignedToWyvern), ("f2", "Uppercase flag", FeatureStatus.Ready));
        var provider = new SequenceProvider(Reply(
            ("cli", "cli-1", "Implement greet.py", "f1"),
            ("docs", "docs-1", "Write README.md usage documentation", null),
            ("cli", "cli-2", "Add --upper flag to greet.py", "f2")));
        var second = NewWyvern(provider);

        await second.AnalyzeProjectAsync(spec);
        var (files, _) = await second.CreateTasksAsync(existingTaskFiles: firstFiles);

        provider.Prompts.First().Should().Contain("[cli-1] Implement greet.py — Done").And.Contain("id: f2");
        var records = AllTaskRecords();
        records.Should().HaveCount(3, "only cli-2 is new: cli-1 exists by id, docs-1 repeats the Done README task");
        records.Single(r => r.Task.StartsWith("[cli-2]")).FeatureId.Should().Be("f2");
        records.Single(r => r.Task.StartsWith("[cli-1]")).Status.Should().Be(TaskStatus.Done);
        files.Keys.Should().Contain(new[] { "cli", "documentation" }).And.NotContain("docs");
        second.AreasWithoutNewTasks.Should().Contain("docs");
    }

    [Fact]
    public void Drake_resolves_a_tasks_branch_from_its_feature()
    {
        var features = new[]
        {
            new Feature { Id = "f1", GitBranch = "feature/f1-greeting-script" },
            new Feature { Id = "f2", GitBranch = null }
        };

        Drake.ResolveFeatureBranch(new TaskRecord { FeatureId = "f1" }, features).Should().Be("feature/f1-greeting-script");
        Drake.ResolveFeatureBranch(new TaskRecord { FeatureId = "f2" }, features).Should().BeNull();
        Drake.ResolveFeatureBranch(new TaskRecord { FeatureId = "nope" }, features).Should().BeNull();
        Drake.ResolveFeatureBranch(new TaskRecord(), features).Should().BeNull();
    }
}
