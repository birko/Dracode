using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The analyzer can ask a person (TASK-170, FEATURE-078 D26): its questions become decisions on the whole project, the
/// project's analysis waits while one is open (the stored decision holds it, so a restart changes nothing), and once they
/// are answered the analysis runs again with the answers and may not ask again. At most three questions, no timeout.
/// </summary>
public class WyvernQuestionsTests : IAsyncLifetime
{
    private const string Questions =
        "{\"projectName\":\"p\",\"questions\":[{\"question\":\"Which storage should the app use?\",\"options\":[\"sqlite\",\"files\"],\"recommended\":\"sqlite\",\"recommendedBecause\":\"one file to back up\"}],\"areas\":[]}";

    private const string Tasks =
        "{\"projectName\":\"p\",\"questions\":[{\"question\":\"Asked again?\",\"options\":[\"yes\",\"no\"],\"recommended\":\"no\",\"recommendedBecause\":\"already answered\"}]," +
        "\"structure\":{\"architectureNotes\":\"one script\"}," + // a planned structure, so no second call asks for one
        "\"areas\":[{\"name\":\"cli\",\"tasks\":[{\"id\":\"cli-1\",\"name\":\"greet\",\"description\":\"Write greet.py\",\"agentType\":\"python\"}]}]}";

    private string _dir = null!;
    private string _db = null!;
    private SqlProjectRepository _repo = null!;
    private KoboldLairConfiguration _config = null!;
    private readonly ScriptedProvider _model = new();
    private readonly List<PendingDecision> _cards = new();

    /// <summary>Answers each analysis with the next scripted reply, and keeps every request it was sent.</summary>
    private sealed class ScriptedProvider : ILlmProvider
    {
        public readonly Queue<string> Replies = new();
        public readonly List<string> Requests = new();
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.Last(m => m.Role == "user").Content?.ToString() ?? "");
            return Task.FromResult(new LlmResponse
            {
                StopReason = "end_turn",
                Content = new List<ContentBlock> { new() { Type = "text", Text = Replies.Dequeue() } }
            });
        }

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_wyvern_questions_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _db = Path.Combine(_dir, "koboldlair.db");
        _repo = new SqlProjectRepository(_db);
        await _repo.InitializeAsync();
        _config = new KoboldLairConfiguration
        {
            ProjectsPath = _dir,
            DefaultProvider = "ollama",
            Providers = new()
            {
                new ProviderConfig
                {
                    Name = "ollama", Type = "ollama", DefaultModel = "m1",
                    IsEnabled = true, RequiresApiKey = false, CompatibleAgents = new() { "all" }, Configuration = new()
                }
            }
        };
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    /// <summary>A server's worth of services over the test database; a second call is a restart.</summary>
    private async Task<(ProjectService Projects, DrakeFactory Drakes, SqlPendingDecisionRepository Decisions)> ServerAsync(bool withStore = true)
    {
        var decisions = new SqlPendingDecisionRepository(_db);
        await decisions.InitializeAsync();
        var providers = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(_config), Path.Combine(_dir, "user-settings.json"));
        var wyverns = new WyvernFactory(providers, _repo, _config) { AnalyzerProviderOverride = () => _model };
        var kobolds = new KoboldFactory(_repo, NullLoggerFactory.Instance, _config);
        var drakes = new DrakeFactory(kobolds, providers, _config, NullLoggerFactory.Instance, projectRepository: _repo,
            pendingDecisions: withStore ? decisions : null)
        {
            OnDecisionRequired = (_, decision) => _cards.Add(decision)
        };
        var projects = new ProjectService(_repo, wyverns, NullLogger<ProjectService>.Instance,
            new GitService(NullLogger<GitService>.Instance), _config, drakes);
        return (projects, drakes, decisions);
    }

    private string NewProject(ProjectService projects)
    {
        var spec = Path.Combine(_dir, "p", "specification.md");
        Directory.CreateDirectory(Path.GetDirectoryName(spec)!);
        File.WriteAllText(spec, "# p\n\nA small app that stores greetings.");
        var project = projects.RegisterProject("p", spec, Guid.NewGuid().ToString());
        project.Status = ProjectStatus.WyrmAssigned;
        _repo.Update(project);
        return project.Id;
    }

    private Project Project(string id) => _repo.GetById(id)!;

    [Fact]
    public async Task Questions_become_decisions_on_the_project_and_its_analysis_waits_without_planning()
    {
        var (projects, _, decisions) = await ServerAsync();
        var id = NewProject(projects);
        _model.Replies.Enqueue(Questions);

        await projects.AnalyzeProjectAsync(id);

        _model.Requests.Should().ContainSingle().Which.Should().Contain("\"questions\"", "an analysis that may ask is told how");
        var open = await decisions.GetUnresolvedForProjectAsync(id);
        open.Should().ContainSingle();
        open[0].TaskId.Should().BeEmpty("the question is the project's, not a task's");
        open[0].Source.Should().Be(PendingDecisionSource.AskHuman);
        open[0].Timeout.Should().BeNull("the project waits until answered");
        AskHumanQuestion.TryParse(open[0])!.Options.Should().Equal("sqlite", "files");
        _cards.Should().ContainSingle(c => c.Id == open[0].Id, "the person gets a card");
        Project(id).Paths.TaskFiles.Should().BeEmpty("nothing is planned while a question is open");
        Project(id).Status.Should().Be(ProjectStatus.WyrmAssigned);
        (await projects.AnalysisWaitsOnPersonAsync(id)).Should().BeTrue();
    }

    [Fact]
    public async Task While_a_question_is_open_the_analysis_does_not_run()
    {
        var (projects, _, _) = await ServerAsync();
        var id = NewProject(projects);
        _model.Replies.Enqueue(Questions);
        await projects.AnalyzeProjectAsync(id);

        await projects.AnalyzeProjectAsync(id);

        _model.Requests.Should().HaveCount(1, "an open question holds the analysis");
    }

    [Fact]
    public async Task Once_answered_the_analysis_runs_again_with_the_answer_plans_and_does_not_ask_again()
    {
        var (projects, drakes, decisions) = await ServerAsync();
        var id = NewProject(projects);
        _model.Replies.Enqueue(Questions);
        await projects.AnalyzeProjectAsync(id);
        var question = (await decisions.GetUnresolvedForProjectAsync(id)).Single();

        (await drakes.ResolvePendingDecisionAsync(id, question.Id, "sqlite")).Should().Be(DecisionAnswerResult.Resolved);
        (await projects.AnalysisWaitsOnPersonAsync(id)).Should().BeFalse();
        _model.Replies.Enqueue(Tasks);
        await projects.AnalyzeProjectAsync(id);

        _model.Requests[1].Should().Contain("Which storage should the app use?").And.Contain("A: sqlite");
        _model.Requests[1].Should().NotContain("QUESTIONS (optional)", "the answered round was the one round");
        (await decisions.GetUnresolvedForProjectAsync(id)).Should().BeEmpty("a reply's questions are ignored once answered");
        Project(id).Paths.TaskFiles.Should().ContainKey("cli");
        Project(id).Status.Should().Be(ProjectStatus.Analyzed);
    }

    [Fact]
    public async Task The_wait_survives_a_restart_and_the_answer_given_after_it_reaches_the_analysis()
    {
        var (before, _, _) = await ServerAsync();
        var id = NewProject(before);
        _model.Replies.Enqueue(Questions);
        await before.AnalyzeProjectAsync(id);

        var (projects, drakes, decisions) = await ServerAsync();

        (await projects.AnalysisWaitsOnPersonAsync(id)).Should().BeTrue("the open question is stored");
        var question = (await decisions.GetUnresolvedForProjectAsync(id)).Single();
        await drakes.ResolvePendingDecisionAsync(id, question.Id, "files");
        _model.Replies.Enqueue(Tasks);
        await projects.AnalyzeProjectAsync(id);
        _model.Requests.Last().Should().Contain("A: files");
        Project(id).Status.Should().Be(ProjectStatus.Analyzed);
    }

    [Fact]
    public async Task At_most_three_questions_are_asked_and_one_that_fails_ask_humans_checks_is_dropped()
    {
        var (projects, _, decisions) = await ServerAsync();
        var id = NewProject(projects);
        _model.Replies.Enqueue("{\"projectName\":\"p\",\"areas\":[],\"questions\":[" +
            "{\"question\":\"Closed with no options?\",\"options\":[],\"allowFreeText\":false}," +
            "{\"question\":\"Q1?\",\"recommended\":\"a\",\"recommendedBecause\":\"b\"},{\"question\":\"Q2?\",\"recommended\":\"a\",\"recommendedBecause\":\"b\"},{\"question\":\"Q3?\",\"recommended\":\"a\",\"recommendedBecause\":\"b\"},{\"question\":\"Q4?\",\"recommended\":\"a\",\"recommendedBecause\":\"b\"}]}");

        await projects.AnalyzeProjectAsync(id);

        (await decisions.GetUnresolvedForProjectAsync(id)).Select(d => AskHumanQuestion.TryParse(d)!.Question)
            .Should().BeEquivalentTo(["Q1?", "Q2?", "Q3?"]);
    }

    [Fact]
    public async Task Without_a_decision_store_the_analysis_is_not_offered_questions_and_plans()
    {
        var (projects, _, _) = await ServerAsync(withStore: false);
        var id = NewProject(projects);
        _model.Replies.Enqueue(Tasks);

        await projects.AnalyzeProjectAsync(id);

        _model.Requests.Single().Should().NotContain("QUESTIONS (optional)");
        Project(id).Status.Should().Be(ProjectStatus.Analyzed);
    }

    [Fact]
    public async Task Answering_a_project_question_says_the_analysis_will_resume()
    {
        var (projects, drakes, decisions) = await ServerAsync();
        var id = NewProject(projects);
        _model.Replies.Enqueue(Questions);
        await projects.AnalyzeProjectAsync(id);
        var question = (await decisions.GetUnresolvedForProjectAsync(id)).Single();
        var desk = new DecisionDesk(decisions, drakes.ResolvePendingDecisionAsync);

        var reply = await desk.AnswerAsync([new DecisionDesk.ProjectRef(id, "p")], question.Id, "1");

        reply.Result.Should().Be(DecisionAnswerResult.Resolved);
        reply.Message.Should().Contain("analysis will resume");
        (await decisions.GetByIdAsync(question.Id))!.Resolution.Should().Be("sqlite", "an option's number is stored as its text");
    }

    [Fact]
    public void Wyrm_still_cannot_ask_a_person()
    {
        var options = new Birko.AI.AgentOptions { WorkingDirectory = _dir, Verbose = false };

        new DraCode.KoboldLair.Agents.WyrmAgent(_model, options).Tools.Select(t => t.Name).Should().NotContain("ask_human");
        new DraCode.KoboldLair.Agents.WyrmPreAnalysisAgent(_model, options).Tools.Select(t => t.Name).Should().NotContain("ask_human");
    }

    // --- through the question chain (TASK-201) -------------------------------------------------------------

    /// <summary>Dragon's level as a test sees it: it answers when told to, and records the answers it hears back.</summary>
    private sealed class DragonLevel(string? answer = null) : IQuestionLevel
    {
        public QuestionLevel Level => QuestionLevel.Dragon;
        public readonly List<string> Heard = new();
        public int Asked;

        public Task<LevelAnswer?> TryAnswerAsync(ChainQuestion question, CancellationToken cancellationToken)
        {
            Asked++;
            return Task.FromResult(answer is null ? null : new LevelAnswer(answer, "the specification says so"));
        }

        public Task OnAnsweredAsync(ChainQuestion question, string answer, QuestionLevel answeredBy, CancellationToken cancellationToken)
        {
            Heard.Add(answer);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task The_analyzers_question_climbs_past_Dragon_to_the_person_and_keeps_its_path_across_a_restart()
    {
        var (projects, drakes, _) = await ServerAsync();
        var dragon = new DragonLevel();
        drakes.Chain = new QuestionChain().Register(dragon);
        var id = NewProject(projects);
        _model.Replies.Enqueue(Questions);

        await projects.AnalyzeProjectAsync(id);

        dragon.Asked.Should().Be(1, "Dragon's level gets the overseer's question first");
        var (_, _, decisions) = await ServerAsync();
        var open = (await decisions.GetUnresolvedForProjectAsync(id)).Single();
        open.Path.Should().Equal(QuestionLevel.Dragon, QuestionLevel.Person);
        _cards.Should().ContainSingle(c => c.Id == open.Id);
    }

    [Fact]
    public async Task The_persons_answer_goes_back_down_to_the_levels_the_question_passed()
    {
        var (projects, drakes, decisions) = await ServerAsync();
        var dragon = new DragonLevel();
        drakes.Chain = new QuestionChain().Register(dragon);
        var id = NewProject(projects);
        _model.Replies.Enqueue(Questions);
        await projects.AnalyzeProjectAsync(id);
        var question = (await decisions.GetUnresolvedForProjectAsync(id)).Single();

        await drakes.ResolvePendingDecisionAsync(id, question.Id, "files");

        dragon.Heard.Should().Equal("files");
    }

    [Fact]
    public async Task A_question_a_level_answers_never_reaches_the_person_and_the_analysis_re_runs_with_that_answer()
    {
        var (projects, drakes, decisions) = await ServerAsync();
        drakes.Chain = new QuestionChain().Register(new DragonLevel("sqlite"));
        var id = NewProject(projects);
        _model.Replies.Enqueue(Questions);

        await projects.AnalyzeProjectAsync(id);

        _cards.Should().OnlyContain(c => c.AwaitsReview, "no question reached the person; only the agent's answer is shown for information (TASK-208)");
        (await decisions.GetUnresolvedForProjectAsync(id)).Should().BeEmpty();
        (await projects.AnalysisWaitsOnPersonAsync(id)).Should().BeFalse();
        var answered = (await decisions.GetProjectLevelAsync(id, DateTime.MinValue)).Single();
        answered.Resolution.Should().Be("sqlite");
        answered.Path.Should().Equal(QuestionLevel.Dragon);

        _model.Replies.Enqueue(Tasks);
        await projects.AnalyzeProjectAsync(id);
        _model.Requests.Last().Should().Contain("A: sqlite");
        Project(id).Status.Should().Be(ProjectStatus.Analyzed);
    }
}
