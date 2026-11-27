using System.Text.Json;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Projects;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// An answer an agent gives instead of the person is recorded as a decision naming the agent and its reason, offered to
/// the person for information, and can be kept or overturned (TASK-208, FEATURE-078 D21). Overturning it on a task
/// reopens the task with its plan revised; on the project, the analysis runs again while no task has started, and is
/// only recorded once one has.
/// </summary>
public class AgentAnswerReviewTests : IAsyncLifetime
{
    private static readonly AskHumanQuestion Asked = new("Which database?", ["SQLite", "Postgres"], false, "the spec names neither");

    private string _dir = null!;
    private SqlProjectRepository _projects = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldLairConfiguration _config = null!;
    private KoboldFactory _kobolds = null!;
    private DrakeFactory _drakes = null!;
    private ProjectService _service = null!;
    private DecisionDesk _desk = null!;
    private readonly List<PendingDecision> _cards = new();
    private readonly List<PendingDecision> _retired = new();

    private sealed class AnsweringLevel(QuestionLevel level, string answer) : IQuestionLevel
    {
        public QuestionLevel Level { get; } = level;
        public Task<LevelAnswer?> TryAnswerAsync(ChainQuestion question, CancellationToken cancellationToken) =>
            Task.FromResult<LevelAnswer?>(new LevelAnswer(answer, "The specification says: use SQLite."));
    }

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_agentreview_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "koboldlair.db");
        _projects = new SqlProjectRepository(db);
        await _projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();
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
        var providers = new ProviderConfigurationService(NullLogger<ProviderConfigurationService>.Instance,
            Options.Create(_config), Path.Combine(_dir, "user-settings.json"));
        _kobolds = new KoboldFactory(_projects, NullLoggerFactory.Instance, _config);
        _drakes = new DrakeFactory(_kobolds, providers, _config, NullLoggerFactory.Instance, projectRepository: _projects,
            pendingDecisions: _decisions)
        {
            // Wyvern answers a Kobold's question; Dragon answers the analyzer's (asked from the Overseer's level)
            Chain = new QuestionChain().Register(new AnsweringLevel(QuestionLevel.Overseer, "SQLite"))
                .Register(new AnsweringLevel(QuestionLevel.Dragon, "SQLite")),
            OnDecisionRequired = (_, d) => _cards.Add(d),
            OnDecisionResolved = (_, d) => _retired.Add(d)
        };
        _service = new ProjectService(_projects, new WyvernFactory(providers, _projects, _config), NullLogger<ProjectService>.Instance,
            new GitService(NullLogger<GitService>.Instance), _config, _drakes);
        _desk = new DecisionDesk(_decisions, _drakes.ResolvePendingDecisionAsync, _drakes.ReviewAgentAnswerAsync);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    /// <summary>A project with one area's task file holding one task in <paramref name="status"/>.</summary>
    private (Project Project, string TaskFile, string TaskId) ProjectWithTask(TaskStatus status, ProjectStatus projectStatus = ProjectStatus.InProgress)
    {
        var folder = Path.Combine(_dir, "notes");
        Directory.CreateDirectory(Path.Combine(folder, "tasks"));
        var spec = Path.Combine(folder, "specification.md");
        File.WriteAllText(spec, "# notes");
        var project = _service.RegisterProject("notes", spec, "owner");
        project.Paths.Output = folder;
        var taskFile = Path.Combine(folder, "tasks", "api-tasks.md");
        var tracker = new TaskTracker();
        var task = tracker.AddTask("[api-1] Store the notes");
        if (status != TaskStatus.Unassigned)
            tracker.UpdateTask(task, status, "csharp");
        tracker.SaveToFile(taskFile);
        project.Paths.TaskFiles["api"] = taskFile;
        project.Status = projectStatus;
        _projects.Update(project);
        return (_projects.GetById(project.Id)!, taskFile, task.Id);
    }

    private static TaskRecord ReadTask(string taskFile, string taskId)
    {
        var tracker = new TaskTracker();
        tracker.LoadFromFile(taskFile);
        return tracker.GetTaskById(taskId)!;
    }

    private async Task<Drake> DrakeHolding(Project project, string taskFile)
    {
        var drake = _drakes.CreateDrake(taskFile, "notes:api", specificationPath: project.Paths.Specification, projectId: project.Id);
        await drake.ReloadTasksFromFileAsync();
        return drake;
    }

    private static DecisionDesk.ProjectRef Ref(Project project) => new(project.Id, project.Name);

    [Fact]
    public async Task An_agents_answer_is_recorded_naming_the_agent_and_its_reason_and_offered_to_the_person_as_a_card()
    {
        var (project, taskFile, taskId) = ProjectWithTask(TaskStatus.Working);
        var drake = await DrakeHolding(project, taskFile);
        var task = drake.GetAllTasks().Single(t => t.Id == taskId);

        var outcome = await drake.AskHumanAsync(task, Asked);

        outcome.Answer.Should().Be("SQLite");
        var decision = (await _decisions.GetByIdAsync(outcome.DecisionId!))!;
        decision.AnsweredBy.Should().Be(QuestionLevel.Overseer);
        decision.AnswerReason.Should().Be("The specification says: use SQLite.");
        decision.AgentAnswer.Should().Be("SQLite");
        decision.AwaitsReview.Should().BeTrue();

        var card = _cards.Should().ContainSingle(c => c.Id == decision.Id).Subject;
        var metadata = PendingDecisionNotification.ToMetadata(card);
        metadata[PendingDecisionNotification.AnsweredBy].Should().Be("Overseer");
        metadata[PendingDecisionNotification.Context].Should().Contain("Wyvern").And.Contain("\"SQLite\"")
            .And.Contain("The specification says: use SQLite.");
        JsonDocument.Parse(metadata[PendingDecisionNotification.ResponseSchema]).RootElement.GetProperty("options")
            .EnumerateArray().Select(o => o.GetString()).Should().Equal(AgentAnswerReview.Keep, "SQLite", "Postgres");

        (await _desk.OpenAsync([Ref(project)])).Should().ContainSingle(o => o.Decision.Id == decision.Id,
            "the person sees it in Dragon (and Warden) until they keep or overturn it");
        task.Status.Should().NotBe(TaskStatus.AwaitingHumanDecision, "a card for information does not hold the task");
    }

    [Fact]
    public async Task Keeping_the_agents_answer_retires_its_card_and_changes_nothing_else()
    {
        var (project, taskFile, taskId) = ProjectWithTask(TaskStatus.Working);
        var drake = await DrakeHolding(project, taskFile);
        var outcome = await drake.AskHumanAsync(drake.GetAllTasks().Single(), Asked);

        var reply = await _desk.AnswerAsync([Ref(project)], outcome.DecisionId!, "1");

        reply.Result.Should().Be(DecisionAnswerResult.Resolved);
        reply.Message.Should().StartWith("Kept the answer Wyvern");
        var decision = (await _decisions.GetByIdAsync(outcome.DecisionId!))!;
        decision.ReviewedAt.Should().NotBeNull();
        decision.Resolution.Should().Be("SQLite");
        decision.IsOverturned.Should().BeFalse();
        _retired.Should().ContainSingle(d => d.Id == decision.Id);
        (await _desk.OpenAsync([Ref(project)])).Should().BeEmpty();
        drake.GetAllTasks().Single(t => t.Id == taskId).Status.Should().NotBe(TaskStatus.AwaitingHumanDecision);
        (await _desk.AnswerAsync([Ref(project)], decision.Id, "Postgres")).Result.Should().Be(DecisionAnswerResult.AlreadyResolved);
    }

    [Fact]
    public async Task Overturning_it_on_a_finished_task_reopens_the_task_revises_its_plan_and_the_kobold_hears_the_persons_answer()
    {
        var (project, taskFile, taskId) = ProjectWithTask(TaskStatus.Working, ProjectStatus.InProgress);
        var drake = await DrakeHolding(project, taskFile);
        var task = drake.GetAllTasks().Single();
        var outcome = await drake.AskHumanAsync(task, Asked);
        drake.UpdateTask(task, TaskStatus.Done);
        await drake.UpdateTasksFileAsync();
        var stored = _projects.GetById(project.Id)!;
        stored.Status = ProjectStatus.Completed;
        _projects.Update(stored);

        var reply = await _desk.AnswerAsync([Ref(project)], outcome.DecisionId!, "Postgres");

        reply.Result.Should().Be(DecisionAnswerResult.Resolved);
        reply.Message.Should().Contain("the task reopens and its plan is revised");
        task.Status.Should().Be(TaskStatus.Unassigned);
        ReadTask(taskFile, taskId).Status.Should().Be(TaskStatus.Unassigned);
        _projects.GetById(project.Id)!.Status.Should().Be(ProjectStatus.InProgress, "a reopened task of a finished project is run");
        var decision = (await _decisions.GetByIdAsync(outcome.DecisionId!))!;
        decision.Resolution.Should().Be("Postgres");
        decision.AgentAnswer.Should().Be("SQLite");
        decision.IsOverturned.Should().BeTrue();
        decision.RevisedAt.Should().NotBeNull("the plan went through the WrongApproach route");
        _retired.Should().ContainSingle(d => d.Id == decision.Id);

        var heard = (await drake.LoadHumanAnswersAsync(taskId))!.Single(a => a.DecisionId == decision.Id).ToMessage();
        heard.Should().Contain("A person overturned the answer Wyvern").And.Contain("\"SQLite\"").And.Contain("Their answer: Postgres");
    }

    [Fact]
    public async Task Overturning_it_with_no_Drake_holding_the_task_reopens_it_in_its_file_and_its_next_Drake_revises_the_plan_first()
    {
        var (project, taskFile, taskId) = ProjectWithTask(TaskStatus.Working);
        var asking = await DrakeHolding(project, taskFile);
        var outcome = await asking.AskHumanAsync(asking.GetAllTasks().Single(), Asked);
        var task = asking.GetAllTasks().Single();
        asking.UpdateTask(task, TaskStatus.Done);
        await asking.UpdateTasksFileAsync();
        _drakes.RemoveAllDrakesForProject(project.Id);

        (await _desk.AnswerAsync([Ref(project)], outcome.DecisionId!, "postgres")).Result.Should().Be(DecisionAnswerResult.Resolved);

        ReadTask(taskFile, taskId).Status.Should().Be(TaskStatus.Unassigned);
        (await _decisions.GetByIdAsync(outcome.DecisionId!))!.RevisedAt.Should().BeNull("no Drake was there to revise it");

        var next = await DrakeHolding(project, taskFile);
        await next.SummonKoboldAsync(next.GetAllTasks().Single(), "csharp");
        (await _decisions.GetByIdAsync(outcome.DecisionId!))!.RevisedAt.Should().NotBeNull("the plan is revised before the run");
    }

    [Fact]
    public async Task An_answer_the_question_does_not_accept_is_refused_and_the_card_stays()
    {
        var (project, taskFile, _) = ProjectWithTask(TaskStatus.Working);
        var drake = await DrakeHolding(project, taskFile);
        var outcome = await drake.AskHumanAsync(drake.GetAllTasks().Single(), Asked);

        var reply = await _desk.AnswerAsync([Ref(project)], outcome.DecisionId!, "MySQL");

        reply.Result.Should().Be(DecisionAnswerResult.InvalidAnswer);
        reply.Message.Should().Contain("1. \"Keep the agent's answer\"");
        (await _decisions.GetByIdAsync(outcome.DecisionId!))!.AwaitsReview.Should().BeTrue();
    }

    private async Task<PendingDecision> ProjectQuestionAnsweredByAgentAsync(Project project) =>
        (await _drakes.AskProjectQuestionsAsync(project.Id, [Asked])).Single();

    [Fact]
    public async Task Overturning_an_answer_to_the_analyzers_question_before_any_task_started_analyzes_the_project_again()
    {
        var (project, taskFile, taskId) = ProjectWithTask(TaskStatus.Unassigned, ProjectStatus.Analyzed);
        var decision = await ProjectQuestionAnsweredByAgentAsync(project);
        decision.AwaitsReview.Should().BeTrue();

        var reply = await _desk.AnswerAsync([Ref(project)], decision.Id, "Postgres");

        reply.Message.Should().Contain("Dragon").And.Contain("the project's analysis runs again");
        var reset = _projects.GetById(project.Id)!;
        reset.Status.Should().Be(ProjectStatus.WyrmAssigned);
        reset.Paths.TaskFiles.Should().BeEmpty();
        reset.Timestamps.AnalyzedAt.Should().BeNull("the next analysis reads every answered question of the project");
        File.Exists(taskFile).Should().BeFalse();
        (await _drakes.ProjectQuestionsAsync(project.Id, DateTime.MinValue)).Single().Resolution.Should().Be("Postgres");
    }

    [Fact]
    public async Task Overturning_an_answer_to_the_analyzers_question_after_a_task_started_is_recorded_and_says_so()
    {
        var (project, taskFile, _) = ProjectWithTask(TaskStatus.Done, ProjectStatus.InProgress);
        var decision = await ProjectQuestionAnsweredByAgentAsync(project);

        // The card numbers "Keep" first: 3 is "Postgres"
        var reply = await _desk.AnswerAsync([Ref(project)], decision.Id, "3");

        reply.Message.Should().Contain("recorded for every later run").And.Contain("1 task(s) had already started");
        _projects.GetById(project.Id)!.Status.Should().Be(ProjectStatus.InProgress);
        File.Exists(taskFile).Should().BeTrue();
        (await _decisions.GetByIdAsync(decision.Id))!.Resolution.Should().Be("Postgres");
    }

    [Fact]
    public void An_agents_answer_is_heard_as_the_agents_and_is_not_passed_on_as_a_persons_words()
    {
        var decision = new PendingDecision
        {
            Source = PendingDecisionSource.AskHuman, Question = Asked.ToJson(), Resolution = "SQLite",
            ResolutionKind = PendingDecisionResolution.Answered, AnsweredBy = QuestionLevel.Supervisor, AgentAnswer = "SQLite"
        };

        HumanAnswer.From(decision).ToMessage().Should().Contain("Drake (the task's supervisor) answered your ask_human question");
        HumanAnswer.CarriesPersonsWords(decision).Should().BeFalse();

        decision.ReviewedAt = DateTime.UtcNow;
        decision.Resolution = "Postgres";
        HumanAnswer.CarriesPersonsWords(decision).Should().BeTrue("once overturned, the answer is the person's");
    }
}
