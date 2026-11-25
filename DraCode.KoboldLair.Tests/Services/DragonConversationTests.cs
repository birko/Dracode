using System.Net.WebSockets;
using Birko.AI;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Server.Models.Dragon;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// The pieces that keep each project's Dragon conversation whole (TASK-151): Dragon's replies are recorded like the
/// user's messages, a saved conversation becomes a well-formed LLM conversation again, and a project switch can wait for
/// the session's running request.
/// </summary>
public class DragonConversationTests
{
    private sealed class CannedProvider(string reply, Task? gate = null) : ILlmProvider
    {
        public string Name => "canned";
        public Action<string, string>? MessageCallback { get; set; }

        public async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            if (gate != null)
                await gate;
            return new LlmResponse
            {
                StopReason = "end_turn",
                Content = new List<ContentBlock> { new() { Type = "text", Text = reply } }
            };
        }

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>A queue for one test. Its Dispose throws when it stops its workers (it waits on a cancelled task), so that is swallowed here.</summary>
    private sealed class QueueScope : IDisposable
    {
        public DragonRequestQueue Queue { get; } = new(NullLogger.Instance);

        public void Dispose() => Queue.Dispose();
    }

    private static DragonRequest Request(string sessionId, DragonAgent dragon, WebSocket socket) => new()
    {
        RequestId = Guid.NewGuid().ToString(),
        SessionId = sessionId,
        Message = "What next?",
        WebSocket = socket,
        CancellationTokenSource = new CancellationTokenSource(),
        QueuedAt = DateTime.UtcNow,
        Dragon = dragon
    };

    private static DragonAgent Dragon(ILlmProvider provider) =>
        new(provider, new AgentOptions { WorkingDirectory = Path.GetTempPath(), Verbose = false });

    [Fact]
    public async Task Dragons_reply_is_handed_to_OnResponse_even_when_the_client_is_gone()
    {
        using var queue = new QueueScope();
        var socket = WebSocket.CreateFromStream(new MemoryStream(), new WebSocketCreationOptions { IsServer = true });
        socket.Abort();

        var recorded = new TaskCompletionSource<(string Reply, bool Streamed)>();
        var request = Request("s-1", Dragon(new CannedProvider("Stripe it is.")), socket);
        request.OnResponse = (reply, streamed) => { recorded.TrySetResult((reply, streamed)); return Task.CompletedTask; };
        await queue.Queue.EnqueueAsync(request);

        var done = await Task.WhenAny(recorded.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        done.Should().Be(recorded.Task, "the reply is recorded whether or not it can be delivered");
        recorded.Task.Result.Reply.Should().Be("Stripe it is.");
    }

    [Fact]
    public async Task A_session_is_idle_only_once_its_running_request_has_finished()
    {
        using var queue = new QueueScope();
        var gate = new TaskCompletionSource();
        var socket = WebSocket.CreateFromStream(new MemoryStream(), new WebSocketCreationOptions { IsServer = true });
        await queue.Queue.EnqueueAsync(Request("s-2", Dragon(new CannedProvider("Done.", gate.Task)), socket));

        (await queue.Queue.WaitForSessionIdleAsync("s-2", TimeSpan.FromMilliseconds(300))).Should().BeFalse("its request is still running");
        (await queue.Queue.WaitForSessionIdleAsync("another-session", TimeSpan.FromMilliseconds(300))).Should().BeTrue();

        gate.SetResult();
        (await queue.Queue.WaitForSessionIdleAsync("s-2", TimeSpan.FromSeconds(10))).Should().BeTrue();
    }

    private static SessionMessage M(string type, string text) => new(Guid.NewGuid().ToString(), type,
        type == "user_message"
            ? new Dictionary<string, object> { ["role"] = "user", ["content"] = text }
            : new Dictionary<string, object> { ["message"] = text },
        DateTime.UtcNow);

    [Fact]
    public void A_saved_conversation_becomes_one_that_starts_with_the_user_alternates_and_ends_with_Dragon()
    {
        var history = new[]
        {
            M("dragon_message", "Welcome!"),               // before the user said anything: dropped
            M("user_message", "Build a shop"),
            M("user_message", "with Stripe"),              // a reply that was never saved: joined to the first
            M("specification_created", "spec"),            // not a turn
            M("dragon_message", "Shop with Stripe, noted."),
            M("project_notification", "Feature ready"),    // not a turn
            M("user_message", "And a blog?"),              // unanswered: dropped, the next message continues the chat
        };

        DragonService.ConversationFromHistory(history).Should().Equal(
            ("user", "Build a shop\n\nwith Stripe"),
            ("assistant", "Shop with Stripe, noted."));
    }

    [Fact]
    public void An_empty_or_reply_less_history_gives_no_conversation()
    {
        DragonService.ConversationFromHistory(Array.Empty<SessionMessage>()).Should().BeEmpty();
        DragonService.ConversationFromHistory(new[] { M("user_message", "Hello?") }).Should().BeEmpty();
    }
}
