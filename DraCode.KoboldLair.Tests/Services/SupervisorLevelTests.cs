using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
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
/// Drake answers a worker's question at the supervisor's level (TASK-203, FEATURE-078 D18): only from the task, its plan,
/// the project's constraints and earlier answers. A question already answered for the task is answered from the record
/// without a model; a model answer is kept only when its quote is in those sources and it is an acceptable answer.
/// Anything else goes up, and with no level above answering, parks the task for the person as before.
/// </summary>
public class SupervisorLevelTests : IAsyncLifetime
{
    private const string ProjectId = "proj-supervisor";
    private const string TaskText = "[api-1] Write the notes store. Use SQLite for storage; the file is notes.db in the workspace.";
    private static readonly AskHumanQuestion Storage = new("Which storage should the notes store use?", ["SQLite", "JSON files"], false, null);

    private string _dir = null!;
    private SqlPendingDecisionRepository _decisions = null!;
    private KoboldFactory _kobolds = null!;
    private readonly ScriptedProvider _model = new();

    /// <summary>Replies with the next scripted text, counting calls; with nothing scripted, a call fails the test.</summary>
    private sealed class ScriptedProvider : ILlmProvider
    {
        public readonly Queue<string> Replies = new();
        public int Calls;
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            Calls++;
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
        _dir = Path.Combine(Path.GetTempPath(), $"dracode_supervisor_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        var db = Path.Combine(_dir, "koboldlair.db");
        var projects = new SqlProjectRepository(db);
        await projects.InitializeAsync();
        _decisions = new SqlPendingDecisionRepository(db);
        await _decisions.InitializeAsync();
        _kobolds = new KoboldFactory(projects, NullLoggerFactory.Instance, new KoboldLairConfiguration { ProjectsPath = _dir });
    }

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, recursive: true); } catch { }
        return Task.CompletedTask;
    }

    private (Drake Drake, TaskRecord Task) NewDrake()
    {
        var tracker = new TaskTracker();
        Drake? drake = null;
        var chain = new QuestionChain().Register(new SupervisorLevel((_, taskId) => drake!.HoldsTask(taskId) ? drake : null, _ => _model));
        drake = new Drake(_kobolds, tracker, Path.Combine(_dir, "backend-tasks.md"), "ollama",
            projectId: ProjectId, pendingDecisions: _decisions) { QuestionChain = chain };
        var task = tracker.AddTask(TaskText);
        tracker.UpdateTask(task, TaskStatus.Working, "csharp");
        return (drake, task);
    }

    private static string Reply(string? answer, string quote) =>
        $"{{\"answer\": {(answer is null ? "null" : $"\"{answer}\"")}, \"quote\": \"{quote}\"}}";

    [Fact]
    public async Task A_question_the_task_settles_is_answered_by_Drake_quoting_it_and_the_task_keeps_running()
    {
        var (drake, task) = NewDrake();
        _model.Replies.Enqueue(Reply("SQLite", "Use SQLite for storage"));

        var outcome = await drake.AskHumanAsync(task, Storage);

        outcome.Answer.Should().Be("SQLite");
        outcome.AnsweredBy.Should().Be(QuestionLevel.Supervisor);
        outcome.Parked.Should().BeFalse();
        task.Status.Should().Be(TaskStatus.Working);
        (await _decisions.GetByIdAsync(outcome.DecisionId!))!.Path.Should().Equal(QuestionLevel.Supervisor);
    }

    [Theory]
    [InlineData("SQLite", "The spec says SQLite everywhere")]   // a quote that is not in the sources
    [InlineData(null, "")]                                       // the sources do not settle it
    [InlineData("Postgres", "Use SQLite for storage")]          // not one of the options
    public async Task An_answer_the_sources_do_not_back_goes_up_and_the_task_waits_for_the_person(string? answer, string quote)
    {
        var (drake, task) = NewDrake();
        _model.Replies.Enqueue(Reply(answer, quote));

        var outcome = await drake.AskHumanAsync(task, Storage);

        outcome.Parked.Should().BeTrue();
        outcome.Answer.Should().BeNull();
        task.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        (await _decisions.GetUnresolvedForTaskAsync(task.Id))!.Path
            .Should().Equal(QuestionLevel.Supervisor, QuestionLevel.Overseer, QuestionLevel.Dragon, QuestionLevel.Person);
    }

    [Fact]
    public async Task A_question_the_task_already_had_answered_is_answered_from_that_record_without_a_model()
    {
        var (drake, task) = NewDrake();
        var open = new AskHumanQuestion("Which port should the server listen on?", [], true, null);
        _model.Replies.Enqueue(Reply(null, ""));
        var first = await drake.AskHumanAsync(task, open);
        (await drake.ResolvePendingDecisionAsync(first.DecisionId!, "8080")).Should().Be(DecisionAnswerResult.Resolved);
        var calls = _model.Calls;

        var again = await drake.AskHumanAsync(task, open with { Question = "  which port should the server listen on? " });

        again.Answer.Should().Be("8080");
        again.AnsweredBy.Should().Be(QuestionLevel.Supervisor);
        _model.Calls.Should().Be(calls, "an answer on record needs no model");
    }

    [Fact]
    public async Task A_question_about_the_whole_project_is_not_the_supervisors_to_answer()
    {
        var (drake, _) = NewDrake();
        var level = new SupervisorLevel((_, _) => drake, _ => _model);

        var answer = await level.TryAnswerAsync(new ChainQuestion(ProjectId, null, QuestionLevel.Worker, Storage), CancellationToken.None);

        answer.Should().BeNull();
        _model.Calls.Should().Be(0);
    }

    [Fact]
    public async Task The_sources_hold_the_task_and_the_answers_it_already_has()
    {
        var (drake, task) = NewDrake();
        _model.Replies.Enqueue(Reply(null, ""));
        var first = await drake.AskHumanAsync(task, Storage);
        await drake.ResolvePendingDecisionAsync(first.DecisionId!, "JSON files");

        var sources = (await drake.SupervisorSourcesAsync(task.Id))!;

        sources.TaskDescription.Should().Be(TaskText);
        sources.EarlierAnswers.Should().ContainSingle(a => a.Question == Storage.Question && a.Answer == "JSON files");
        sources.Render().Should().Contain("Use SQLite for storage").And.Contain("A: JSON files");
        (await drake.SupervisorSourcesAsync("not-held")).Should().BeNull();
    }

    [Fact]
    public async Task The_sources_take_the_tasks_full_description_and_the_constraints_from_the_saved_analysis()
    {
        // The task record keeps only the title; Wyvern's analysis beside tasks/ has the description and the constraints
        var project = Path.Combine(_dir, "greet");
        Directory.CreateDirectory(Path.Combine(project, "tasks"));
        File.WriteAllText(Path.Combine(project, "analysis.json"),
            "{\"areas\":[{\"name\":\"cli\",\"tasks\":[{\"id\":\"cli-1\",\"name\":\"greet\",\"description\":\"Write greet.py. The greeting language is Slovak (Ahoj).\"}]}]," +
            "\"constraints\":[\"Only one script\"],\"outOfScope\":[\"A GUI\"]}");
        var tracker = new TaskTracker();
        var drake = new Drake(_kobolds, tracker, Path.Combine(project, "tasks", "cli-tasks.md"), "ollama",
            projectId: ProjectId, pendingDecisions: _decisions);
        var task = tracker.AddTask("[cli-1] Write greet.py");

        var sources = (await drake.SupervisorSourcesAsync(task.Id))!;

        sources.TaskDescription.Should().Contain("The greeting language is Slovak (Ahoj).");
        sources.Constraints.Should().Contain("Only one script").And.Contain("OUT OF SCOPE: A GUI");
    }
}
