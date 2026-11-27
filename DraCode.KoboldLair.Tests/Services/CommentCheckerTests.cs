using System.Diagnostics;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Models.Tasks;
using DraCode.KoboldLair.Services.Checks;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The comment checker (TASK-220, FEATURE-078 D8; the lifecycle's review-comments): comments the task's change added or
/// changed that contradict the code (stale) or carry what already lives elsewhere (redundant), each named with where its
/// content lives. Read from the change (git's diff) and the files it touched; never a finding for length alone.
/// </summary>
public class CommentCheckerTests : IDisposable
{
    private const string Task = "[cli-3] Add a retry helper (retry.py). Must implement: retry(fn, attempts) calls fn until it succeeds";

    private readonly string _workspace = Path.Combine(Path.GetTempPath(), $"dracode_comments_{Guid.NewGuid():N}");

    public CommentCheckerTests() => Directory.CreateDirectory(_workspace);

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
    public async System.Threading.Tasks.Task It_reads_the_change_and_reports_stale_and_redundant_comments_with_where_their_content_lives()
    {
        Git("init -q");
        Git("config user.email t@example.com");
        Git("config user.name t");
        Write("retry.py", "def retry(fn, attempts):\n    return fn()\n");
        Git("add .");
        Git("commit -qm base");
        Write("retry.py", "# Retries fn up to 5 times\ndef retry(fn, attempts):\n    # loop over the attempts\n" +
            "    for _ in range(attempts):\n        try:\n            return fn()\n        except Exception:\n            pass\n");
        var model = new ScriptedProvider("""
            { "findings": [
              { "kind": "stale", "text": "The comment says 5 tries; the code tries `attempts` times", "evidence": "retry.py:1 \"Retries fn up to 5 times\" — the code: range(attempts)" },
              { "kind": "redundant", "text": "Restates the loop below it", "evidence": "retry.py:3 \"loop over the attempts\" — lives in: the code, retry.py:4" } ] }
            """);

        var findings = await new CommentChecker(_ => model).CheckAsync(Input());

        findings.Select(f => (f.Checker, f.Kind)).Should().Equal(("comments", "stale"), ("comments", "redundant"));
        findings[1].Evidence.Should().Contain("lives in");
        model.SystemPrompt.Should().Contain("the comment checker").And.Contain("\"stale\"").And.Contain("\"redundant\"")
            .And.Contain("length", "a comment is never a finding for being long");
        model.Message.Should().Contain(Task).And.Contain("+# Retries fn up to 5 times", "the diff shows which comments the change wrote")
            .And.Contain("#### retry.py");
    }

    [Fact]
    public async System.Threading.Tasks.Task It_runs_last_of_the_checkers_and_a_change_with_honest_comments_passes()
    {
        Write("retry.py", "def retry(fn, attempts):\n    return fn()\n");
        var checker = new CommentChecker(_ => new ScriptedProvider("""{ "findings": [] }"""));

        checker.Cost.Should().BeGreaterThan(new CorrectnessChecker(_ => null).Cost);
        (await checker.CheckAsync(Input())).Should().BeEmpty();
    }

    [Fact]
    public async System.Threading.Tasks.Task With_no_model_the_check_cannot_run_rather_than_pass()
    {
        var check = () => new CommentChecker(_ => null).CheckAsync(Input());

        await check.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no model*");
    }
}
