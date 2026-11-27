using System.Diagnostics;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services.Checks;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The correctness checker (TASK-221, FEATURE-078 D8): a review of a finished task's change for likely bugs, each named
/// with its file and line and the failure it would cause, read from the change (git's diff) and the files it touched.
/// </summary>
public class CorrectnessCheckerTests : IDisposable
{
    private const string Task = "[cli-2] Add an average to stats.py (stats.py). Must implement: average(values) returns the mean";

    private readonly string _workspace = Path.Combine(Path.GetTempPath(), $"dracode_correct_{Guid.NewGuid():N}");

    public CorrectnessCheckerTests() => Directory.CreateDirectory(_workspace);

    public void Dispose()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_workspace, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
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

    private TaskCheckInput Input() => new(new TaskRecord { Id = Guid.NewGuid().ToString(), Task = Task }, _workspace, "p");

    [Fact]
    public async System.Threading.Tasks.Task It_reads_the_change_as_a_diff_and_the_files_the_task_touched()
    {
        Git("init -q");
        Git("config user.email t@example.com");
        Git("config user.name t");
        Write("stats.py", "def total(values):\n    return sum(values)\n");
        Write("untouched.py", "print('not the task')\n");
        Git("add .");
        Git("commit -qm base");
        Write("stats.py", "def total(values):\n    return sum(values)\n\ndef average(values):\n    return total(values) / len(values)\n");
        Write("report.py", "from stats import average\nprint(average([]))\n"); // new, untracked

        var material = await CorrectnessChecker.MaterialAsync(Input());

        material.Should().Contain("+def average(values):", "the diff shows what the task changed")
            .And.Contain("#### stats.py").And.Contain("return sum(values)", "the whole file gives the change its context")
            .And.Contain("#### report.py").And.Contain("print(average([]))")
            .And.NotContain("untouched.py");
    }

    [Fact]
    public async System.Threading.Tasks.Task Outside_git_it_reads_the_files_the_task_named()
    {
        Write("stats.py", "def average(values):\n    return sum(values) / len(values)\n");

        var material = await CorrectnessChecker.MaterialAsync(Input());

        material.Should().Contain("#### stats.py").And.Contain("sum(values) / len(values)").And.Contain("(no diff");
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
    public async System.Threading.Tasks.Task It_reports_a_likely_bug_with_its_line_and_the_failure_it_causes()
    {
        Write("stats.py", "def average(values):\n    return sum(values) / len(values)\n");
        var model = new ScriptedProvider("""
            { "findings": [
              { "kind": "bug", "text": "average([]) divides by zero", "evidence": "stats.py:2 — an empty list raises ZeroDivisionError" } ] }
            """);
        var checker = new CorrectnessChecker(_ => model);

        var findings = await checker.CheckAsync(Input());

        var finding = findings.Should().ContainSingle().Subject;
        (finding.Checker, finding.Kind).Should().Be(("correctness", "bug"));
        finding.Evidence.Should().Contain("stats.py:2");
        model.SystemPrompt.Should().Contain("the correctness checker").And.Contain("\"bug\"");
        model.Message.Should().Contain(Task).And.Contain("sum(values) / len(values)");
    }

    [Fact]
    public async System.Threading.Tasks.Task It_runs_after_the_intent_check_and_a_clean_change_passes()
    {
        Write("stats.py", "def average(values):\n    return sum(values) / len(values) if values else 0\n");
        var checker = new CorrectnessChecker(_ => new ScriptedProvider("""{ "findings": [] }"""));

        checker.Cost.Should().BeGreaterThan(new IntentChecker(_ => null).Cost);
        (await checker.CheckAsync(Input())).Should().BeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task With_no_model_the_check_cannot_run_rather_than_pass()
    {
        var check = () => new CorrectnessChecker(_ => null).CheckAsync(Input());

        await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no model*");
    }
}
