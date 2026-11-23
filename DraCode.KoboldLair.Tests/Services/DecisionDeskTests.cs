using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Factories;
using DraCode.KoboldLair.Models.Configuration;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Orchestrators;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The decision desk answers and lists a person's pending decisions for the projects they can see (TASK-024,
/// FEATURE-030 D4–D6): the stored decision is the source of truth, a second answer is a no-op that says so, and a
/// decision outside the caller's projects reads as unknown.
/// </summary>
public class DecisionDeskTests : IAsyncLifetime
{
    private string _dir = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private const string ProjectId = "proj-desk";
    private const string OtherProjectId = "proj-other";

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_desk_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "koboldlair.db");
        var projects = new SqlProjectRepository(db);
        await projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();

        var config = new KoboldLairConfiguration
        {
            ProjectsPath = _dir,
            DefaultProvider = "ollama",
            Providers = new()
            {
                new ProviderConfig
                {
                    Name = "ollama", Type = "ollama", DefaultModel = "m1",
                    IsEnabled = true, RequiresApiKey = false, CompatibleAgents = new() { "all" },
                    Configuration = new()
                }
            }
        };
        _kobolds = new KoboldFactory(projects, NullLoggerFactory.Instance, config);
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private (Drake drake, TaskTracker tracker) NewDrake(string projectId = ProjectId)
    {
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(_dir, $"{projectId}-tasks.md"), "ollama",
            projectId: projectId, pendingDecisions: _decisions);
        return (drake, tracker);
    }

    private static TaskRecord Working(TaskTracker tracker, string text = "[api-1] Pick the storage engine")
    {
        var task = tracker.AddTask(text);
        tracker.UpdateTask(task, TaskStatus.Working, "csharp");
        return task;
    }

    private static AskHumanQuestion Question(params string[] options) =>
        new("Which database?", options, AllowFreeText: options.Length == 0, Context: "the spec names neither");

    private DecisionDesk Desk(params Drake[] drakes) => new(_decisions, async (projectId, decisionId, answer) =>
    {
        foreach (var drake in drakes)
        {
            var result = await drake.ResolvePendingDecisionAsync(decisionId, answer);
            if (result != DecisionAnswerResult.NotFound)
                return result;
        }
        return DecisionAnswerResult.NotFound;
    });

    private static readonly DecisionDesk.ProjectRef Mine = new(ProjectId, "My project");
    private static readonly DecisionDesk.ProjectRef Theirs = new(OtherProjectId, "Their project");

    [Fact]
    public async Task An_answer_through_the_desk_resumes_the_task_and_the_next_Kobold_hears_it()
    {
        var (drake, tracker) = NewDrake();
        var task = Working(tracker);
        var asked = await drake.AskHumanAsync(task, Question("SQLite", "Postgres"));

        var reply = await Desk(drake).AnswerAsync(new[] { Mine }, asked.DecisionId!, "Postgres");

        reply.Result.Should().Be(DecisionAnswerResult.Resolved);
        reply.Message.Should().Contain("resume");
        task.Status.Should().NotBe(TaskStatus.AwaitingHumanDecision);
        var kobold = (await drake.SummonKoboldAsync(task, "csharp"))!;
        kobold.HumanAnswers.Should().ContainSingle().Which.Answer.Should().Be("Postgres");
    }

    [Fact]
    public async Task A_typed_answer_naming_one_option_resolves_to_it_and_one_naming_two_is_refused_with_the_options_numbered()
    {
        // FEATURE-028 D9 (TASK-176)
        var (drake, tracker) = NewDrake();
        var task = Working(tracker);
        var asked = await drake.AskHumanAsync(task, Question("Postgres (managed)", "Postgres (self-hosted)", "SQLite"));
        var desk = Desk(drake);

        var ambiguous = await desk.AnswerAsync(new[] { Mine }, asked.DecisionId!, "postgres");
        ambiguous.Result.Should().Be(DecisionAnswerResult.InvalidAnswer);
        ambiguous.Message.Should().Contain("more than one option")
            .And.Contain("1. \"Postgres (managed)\"").And.Contain("2. \"Postgres (self-hosted)\"").And.Contain("3. \"SQLite\"")
            .And.Contain("still open");
        var none = await desk.AnswerAsync(new[] { Mine }, asked.DecisionId!, "MySQL");
        none.Message.Should().Contain("is not one of the options").And.Contain("3. \"SQLite\"");

        var reply = await desk.AnswerAsync(new[] { Mine }, asked.DecisionId!, "2");
        reply.Result.Should().Be(DecisionAnswerResult.Resolved);
        (await _decisions.GetByIdAsync(asked.DecisionId!))!.Resolution.Should().Be("Postgres (self-hosted)", "the option's full text is stored");
    }

    [Fact]
    public async Task A_second_answer_changes_nothing_and_says_the_decision_is_already_resolved()
    {
        var (drake, tracker) = NewDrake();
        var task = Working(tracker);
        var asked = await drake.AskHumanAsync(task, Question("SQLite", "Postgres"));
        var desk = Desk(drake);
        await desk.AnswerAsync(new[] { Mine }, asked.DecisionId!, "Postgres");

        var again = await desk.AnswerAsync(new[] { Mine }, asked.DecisionId!, "SQLite");

        again.Result.Should().Be(DecisionAnswerResult.AlreadyResolved);
        again.Message.Should().Contain("already answered").And.Contain("nothing changed");
        (await _decisions.GetByIdAsync(asked.DecisionId!))!.Resolution.Should().Be("Postgres");
    }

    [Fact]
    public async Task An_answer_that_is_not_an_option_names_the_options_and_leaves_the_question_open()
    {
        var (drake, tracker) = NewDrake();
        var task = Working(tracker);
        var asked = await drake.AskHumanAsync(task, Question("SQLite", "Postgres"));

        var reply = await Desk(drake).AnswerAsync(new[] { Mine }, asked.DecisionId!, "MySQL");

        reply.Result.Should().Be(DecisionAnswerResult.InvalidAnswer);
        reply.Message.Should().Contain("SQLite").And.Contain("Postgres");
        (await _decisions.GetByIdAsync(asked.DecisionId!))!.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task A_decision_of_a_project_the_caller_cannot_see_reads_as_unknown_and_stays_open()
    {
        var (drake, tracker) = NewDrake(OtherProjectId);
        var task = Working(tracker);
        var asked = await drake.AskHumanAsync(task, Question("SQLite", "Postgres"));

        var reply = await Desk(drake).AnswerAsync(new[] { Mine }, asked.DecisionId!, "Postgres");

        reply.Result.Should().Be(DecisionAnswerResult.NotFound);
        reply.Message.Should().NotContain("Which database");
        (await _decisions.GetByIdAsync(asked.DecisionId!))!.IsResolved.Should().BeFalse();
    }

    [Fact]
    public async Task Open_decisions_are_listed_for_the_visible_projects_only_and_leave_once_answered()
    {
        var (drake, tracker) = NewDrake();
        var (otherDrake, otherTracker) = NewDrake(OtherProjectId);
        var first = await drake.AskHumanAsync(Working(tracker), Question("SQLite", "Postgres"));
        var second = await drake.AskHumanAsync(Working(tracker, "[api-2] Pick the queue"), Question());
        await otherDrake.AskHumanAsync(Working(otherTracker), Question());
        var desk = Desk(drake, otherDrake);

        var open = await desk.OpenAsync(new[] { Mine });

        open.Select(o => o.Decision.Id).Should().BeEquivalentTo(new[] { first.DecisionId, second.DecisionId });
        open.Should().OnlyContain(o => o.Project == Mine);

        await desk.AnswerAsync(new[] { Mine }, first.DecisionId!, "Postgres");
        (await desk.OpenAsync(new[] { Mine })).Select(o => o.Decision.Id).Should().Equal(second.DecisionId);
        (await desk.OpenAsync(new[] { Mine, Theirs })).Should().HaveCount(2);
    }

    [Fact]
    public async Task Without_a_decision_store_nothing_is_open_and_an_answer_reads_as_unknown()
    {
        var desk = new DecisionDesk(null, (_, _, _) => Task.FromResult(DecisionAnswerResult.Resolved));

        (await desk.OpenAsync(new[] { Mine })).Should().BeEmpty();
        (await desk.AnswerAsync(new[] { Mine }, "dec-1", "x")).Result.Should().Be(DecisionAnswerResult.NotFound);
    }
}
