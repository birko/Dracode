using System.Diagnostics;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services.Checks;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The intent checker (TASK-218, FEATURE-078 D8): did a finished task build what its description asked — missing, extra
/// and wrongly built requirements, each quoting the requirement — read from the task, the answers given for it and the
/// files it touched. A task with no findings passes.
/// </summary>
public class IntentCheckerTests : IDisposable
{
    private const string Task =
        "[cli-1] Create menu.py (menu.py). Must implement: prints three dishes with their prices in EUR; " +
        "Acceptance criteria: each line reads 'dish — price EUR'";

    private readonly string _workspace = Path.Combine(Path.GetTempPath(), $"dracode_intent_{Guid.NewGuid():N}");

    public IntentCheckerTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_workspace, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal); // git leaves its objects read-only
            Directory.Delete(_workspace, recursive: true);
        }
        catch { }
    }

    private void Write(string path, string content)
    {
        var full = Path.Combine(_workspace, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    private void Git(string args)
    {
        using var git = Process.Start(new ProcessStartInfo("git", args)
        {
            WorkingDirectory = _workspace, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
        })!;
        git.WaitForExit();
        git.ExitCode.Should().Be(0, $"git {args}");
    }

    private TaskCheckInput Input(IReadOnlyList<string>? planned = null, IReadOnlyList<string>? answers = null) =>
        new(new TaskRecord { Id = Guid.NewGuid().ToString(), Task = Task }, _workspace, "p", planned, answers);

    [Fact]
    public async System.Threading.Tasks.Task It_reads_the_answers_and_the_files_the_task_named_planned_and_changed()
    {
        Git("init -q");
        Git("config user.email t@example.com");
        Git("config user.name t");
        Write("old.py", "print('committed before the task')\n");
        Git("add .");
        Git("commit -qm base");
        Write("menu.py", "print('Soup — 3 EUR')\n");                 // the description's target
        Write("lib/prices.py", "PRICES = {'Soup': 3}\n");               // in the plan
        Write("extras/banner.py", "print('Welcome!')\n");              // only git knows: new, untracked
        Write("image.png", "\0\0binary");                              // changed, but not text

        var material = await IntentChecker.MaterialAsync(Input(["lib/prices.py", "lib/missing.py", "../outside.py"],
            ["Which currency? → EUR"]));

        material.Should().Contain("- Which currency? → EUR")
            .And.Contain("#### menu.py").And.Contain("Soup — 3 EUR")
            .And.Contain("#### lib/prices.py").And.Contain("PRICES")
            .And.Contain("#### extras/banner.py").And.Contain("Welcome!")
            .And.Contain("#### lib/missing.py\n(does not exist)".Replace("\n", Environment.NewLine))
            .And.NotContain("old.py", "a file committed before the task is not its work")
            .And.NotContain("outside.py").And.NotContain("image.png");
    }

    [Fact]
    public async System.Threading.Tasks.Task Outside_git_it_reads_the_named_and_planned_files_and_caps_what_it_reads()
    {
        Write("menu.py", new string('x', IntentChecker.MaxFileChars + 500));

        var material = await IntentChecker.MaterialAsync(Input());

        material.Should().Contain("#### menu.py").And.Contain("… (cut here)").And.Contain("(none)", "no answer was given");
        material.Length.Should().BeLessThan(IntentChecker.MaxFileChars + 1_000);
    }

    private sealed class ScriptedProvider(string reply) : ILlmProvider
    {
        public string? SystemPrompt;
        public string? Message;
        public string Name => "scripted";
        public Action<string, string>? MessageCallback { get; set; }

        public System.Threading.Tasks.Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            SystemPrompt = systemPrompt;
            Message = messages.Last().Content?.ToString();
            return System.Threading.Tasks.Task.FromResult(new LlmResponse
            {
                StopReason = "end_turn",
                Content = new List<ContentBlock> { new() { Type = "text", Text = reply } }
            });
        }

        public System.Threading.Tasks.Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Fact]
    public async System.Threading.Tasks.Task It_reports_missing_extra_and_wrong_requirements_each_quoting_it()
    {
        Write("menu.py", "print('Soup 3')\nprint('Welcome!')\n");
        var model = new ScriptedProvider("""
            { "findings": [
              { "kind": "missing", "text": "Only one dish is printed", "evidence": "\"prints three dishes with their prices in EUR\" — menu.py" },
              { "kind": "wrong", "text": "The price has no currency", "evidence": "\"each line reads 'dish — price EUR'\" — menu.py:1" },
              { "kind": "extra", "text": "Prints a welcome line nobody asked for", "evidence": "menu.py:2" } ] }
            """);
        var checker = new IntentChecker(_ => model);

        var findings = await checker.CheckAsync(Input());

        findings.Select(f => (f.Checker, f.Kind)).Should().Equal(("intent", "missing"), ("intent", "wrong"), ("intent", "extra"));
        findings[0].Evidence.Should().Contain("prints three dishes with their prices in EUR");
        model.SystemPrompt.Should().Contain("the intent checker").And.Contain("\"missing\"").And.Contain("\"extra\"").And.Contain("\"wrong\"");
        model.Message.Should().Contain(Task).And.Contain("print('Soup 3')");
    }

    [Fact]
    public async System.Threading.Tasks.Task A_task_with_no_findings_passes()
    {
        Write("menu.py", "print('Soup — 3 EUR')\n");
        var checker = new IntentChecker(_ => new ScriptedProvider("""{ "findings": [] }"""));

        (await checker.CheckAsync(Input())).Should().BeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task With_no_model_the_check_cannot_run_rather_than_pass()
    {
        var checker = new IntentChecker(_ => null);

        var check = () => checker.CheckAsync(Input());

        await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no model*");
    }
}
