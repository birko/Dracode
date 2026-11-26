using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Wyvern as the project's overseer (TASK-204, FEATURE-078 D25): it works over several turns with read-only access to the
/// project folder (specification, analysis, task lists, workspace) and the project's decisions, and no write or command
/// tools; its analysis still ends in the same JSON, and it can be consulted outside an analysis.
/// </summary>
public class WyvernOverseerTests : IAsyncLifetime
{
    private const string Plan =
        "{\"projectName\":\"p\",\"structure\":{\"architectureNotes\":\"one script\"}," +
        "\"areas\":[{\"name\":\"cli\",\"tasks\":[{\"id\":\"cli-1\",\"name\":\"greet\",\"description\":\"Write greet.py\",\"agentType\":\"python\"}]}]}";

    private string _dir = null!;
    private string _project = null!;
    private readonly ScriptedProvider _model = new();

    /// <summary>Replies in order; records every request's messages.</summary>
    private sealed class ScriptedProvider : ILlmProvider
    {
        public readonly Queue<LlmResponse> Replies = new();
        public readonly List<List<Message>> Requests = new();
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            return Task.FromResult(Replies.Dequeue());
        }

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private static LlmResponse ToolUse(string name, Dictionary<string, object> input) => new()
    {
        StopReason = "tool_use",
        Content = new List<ContentBlock> { new() { Type = "tool_use", Id = Guid.NewGuid().ToString("N"), Name = name, Input = input } }
    };

    private static LlmResponse Text(string text) => new()
    {
        StopReason = "end_turn",
        Content = new List<ContentBlock> { new() { Type = "text", Text = text } }
    };

    public Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_overseer_{Guid.NewGuid():N}");
        _project = Path.Combine(_dir, "p");
        Directory.CreateDirectory(Path.Combine(_project, "workspace"));
        File.WriteAllText(Path.Combine(_project, "specification.md"), "# p\n\nThe greeting is Slovak: Ahoj.");
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private static string AllText(IEnumerable<Message> messages) =>
        string.Join("\n", messages.Select(m => System.Text.Json.JsonSerializer.Serialize(m.Content)));

    [Fact]
    public void Wyvern_has_read_only_tools_and_no_write_or_command_ones()
    {
        var agent = new WyvernAgent(_model, new AgentOptions { WorkingDirectory = _project, Verbose = false },
            decisions: () => Task.FromResult<IReadOnlyList<PendingDecision>>(Array.Empty<PendingDecision>()));

        agent.Tools.Select(t => t.Name).Should().BeEquivalentTo(["list_files", "read_file", "search_code", "project_decisions"]);
    }

    [Fact]
    public async Task The_analysis_can_look_things_up_over_several_turns_and_still_ends_in_its_JSON()
    {
        var config = new KoboldLairConfiguration
        {
            ProjectsPath = _dir, DefaultProvider = "ollama",
            Providers = new() { new ProviderConfig { Name = "ollama", Type = "ollama", DefaultModel = "m1", IsEnabled = true,
                RequiresApiKey = false, CompatibleAgents = new() { "all" }, Configuration = new() } }
        };
        var repo = new SqlProjectRepository(Path.Combine(_dir, "koboldlair.db"));
        await repo.InitializeAsync();
        var providers = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(config), Path.Combine(_dir, "user-settings.json"));
        var wyvern = new WyvernFactory(providers, repo, config) { AnalyzerProviderOverride = () => _model }
            .CreateWyvern("p", Path.Combine(_project, "specification.md"), _project);
        // First it reads the specification from the project folder, then it answers
        _model.Replies.Enqueue(ToolUse("read_file", new Dictionary<string, object> { ["file_path"] = "specification.md" }));
        _model.Replies.Enqueue(Text(Plan));

        var analysis = await wyvern.AnalyzeProjectAsync();

        analysis.Areas.SelectMany(a => a.Tasks).Should().ContainSingle(t => t.Id == "cli-1");
        _model.Requests.Should().HaveCount(2, "one turn to look, one to answer");
        AllText(_model.Requests[1]).Should().Contain("The greeting is Slovak: Ahoj.", "the tool read the project folder");
    }

    [Fact]
    public async Task Wyvern_can_be_consulted_outside_an_analysis_and_sees_the_projects_decisions()
    {
        var decided = new PendingDecision
        {
            ProjectId = "p", TaskId = "", Source = PendingDecisionSource.AskHuman,
            Question = new AskHumanQuestion("Which storage?", [], true, null).ToJson(),
            Resolution = "SQLite", ResolutionKind = PendingDecisionResolution.Answered, ResolvedAt = DateTime.UtcNow
        };
        var agent = new WyvernAgent(_model, new AgentOptions { WorkingDirectory = _project, Verbose = false },
            decisions: () => Task.FromResult<IReadOnlyList<PendingDecision>>([decided]));
        _model.Replies.Enqueue(ToolUse("project_decisions", new Dictionary<string, object>()));
        _model.Replies.Enqueue(Text("Storage is SQLite, as decided."));

        var reply = await agent.ConsultAsync("Which storage did the project decide on?");

        reply.Should().Be("Storage is SQLite, as decided.");
        AllText(_model.Requests[1]).Should().Contain("Which storage?").And.Contain("SQLite");
    }
}
