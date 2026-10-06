using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Orchestrators;
using FluentAssertions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// A Wyvern reply that is empty, or parses but carries no tasks, must fail the analysis rather than pass as
/// "nothing to do" — that left projects Analyzed with 0 tasks and no error (TASK-098 / FIELD-013).
/// </summary>
public class WyvernUnusableReplyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"dracode_wyvern_reply_test_{Guid.NewGuid():N}");

    public WyvernUnusableReplyTests()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "specification.md"), "# p\n\nA script greet.py that prints Hello.");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private sealed class CannedProvider(string reply) : ILlmProvider
    {
        public string Name => "canned";
        public Action<string, string>? MessageCallback { get; set; }

        public Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new LlmResponse
            {
                StopReason = "end_turn",
                Content = new List<ContentBlock> { new() { Type = "text", Text = reply } }
            });

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private Wyvern WyvernReplying(string reply)
    {
        var options = new AgentOptions { WorkingDirectory = _dir, Verbose = false };
        return new Wyvern("p", Path.Combine(_dir, "specification.md"), new WyvernAgent(new CannedProvider(reply), options),
            "canned", new Dictionary<string, string>(), options, _dir);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("Here is the plan: {\"note\": \"see below\"}")]
    public async Task An_empty_or_task_less_reply_fails_the_analysis(string reply)
    {
        var analyze = () => WyvernReplying(reply).AnalyzeProjectAsync();

        await analyze.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task A_reply_with_tasks_is_accepted()
    {
        const string reply = "{\"projectName\":\"p\",\"areas\":[{\"name\":\"cli\",\"tasks\":[{\"id\":\"cli-1\",\"name\":\"greet\",\"description\":\"Write greet.py\",\"agentType\":\"python\"}]}]}";

        var analysis = await WyvernReplying(reply).AnalyzeProjectAsync();

        analysis.Areas.SelectMany(a => a.Tasks).Should().ContainSingle(t => t.Id == "cli-1");
    }
}
