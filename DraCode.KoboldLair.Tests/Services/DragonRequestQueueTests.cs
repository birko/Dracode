using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Birko.AI.Models;
using Birko.AI.Providers;
using Birko.AI.Tools;
using Birko.AI;
using DraCode.KoboldLair.Agents;
using DraCode.KoboldLair.Server.Models.Dragon;
using DraCode.KoboldLair.Server.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// A session's Dragon requests run one at a time, in the order they came (TASK-187). A message sent while Dragon is still
/// answering used to cancel the waiting one, or run beside the running one on the same Dragon, whose conversation then
/// lost one of the two turns.
/// </summary>
public class DragonRequestQueueTests
{
    /// <summary>Answers each call after a delay, recording the user turns it was given and how many calls overlap.</summary>
    private sealed class SlowProvider(TimeSpan delay) : ILlmProvider
    {
        private int _running;
        public int MaxConcurrent;
        public readonly List<List<string>> UserTurnsPerCall = new();

        public string Name => "slow";
        public Action<string, string>? MessageCallback { get; set; }

        public async Task<LlmResponse> SendMessageAsync(List<Message> messages, List<Tool> tools, string systemPrompt,
            CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _running);
            lock (UserTurnsPerCall)
            {
                MaxConcurrent = Math.Max(MaxConcurrent, now);
                UserTurnsPerCall.Add(messages.Where(m => m.Role == "user").Select(m => Text(m.Content)).ToList());
            }
            try
            {
                await Task.Delay(delay, cancellationToken);
                var last = messages.Last(m => m.Role == "user");
                return new LlmResponse
                {
                    StopReason = "end_turn",
                    Content = [new ContentBlock { Type = "text", Text = $"reply to {Text(last.Content)}" }]
                };
            }
            finally
            {
                Interlocked.Decrement(ref _running);
            }
        }

        public Task<LlmStreamingResponse> SendMessageStreamingAsync(List<Message> messages, List<Tool> tools,
            string systemPrompt, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static string Text(object? content) => content switch
        {
            string s => s,
            null => "",
            _ => JsonSerializer.Serialize(content)
        };
    }

    /// <summary>An open socket that keeps what is sent to it.</summary>
    private sealed class RecordingSocket : WebSocket
    {
        public readonly List<JsonElement> Sent = new();
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => WebSocketState.Open;
        public override string? SubProtocol => null;
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus s, string? d, CancellationToken c) => Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus s, string? d, CancellationToken c) => Task.CompletedTask;
        public override void Dispose() { }
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> b, CancellationToken c) =>
            throw new NotSupportedException();

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool end, CancellationToken c)
        {
            lock (Sent)
                Sent.Add(JsonDocument.Parse(Encoding.UTF8.GetString(buffer)).RootElement.Clone());
            return Task.CompletedTask;
        }

        public List<string> Replies()
        {
            lock (Sent)
                return Sent.Where(m => m.GetProperty("type").GetString() == "dragon_message")
                    .Select(m => m.GetProperty("message").GetString()!).ToList();
        }
    }

    private static DragonRequestQueue NewQueue() => new(NullLogger.Instance);

    private static DragonRequest Request(string sessionId, string message, DragonAgent dragon, WebSocket socket) => new()
    {
        RequestId = Guid.NewGuid().ToString(),
        SessionId = sessionId,
        Message = message,
        WebSocket = socket,
        CancellationTokenSource = new CancellationTokenSource(),
        QueuedAt = DateTime.UtcNow,
        Dragon = dragon
    };

    private static async Task WaitUntil(Func<bool> condition, int seconds = 15)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(until, "the condition should hold within {0} s", seconds);
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task A_message_sent_while_Dragon_answers_is_answered_after_it_with_both_in_the_conversation()
    {
        var provider = new SlowProvider(TimeSpan.FromMilliseconds(400));
        var dragon = new DragonAgent(provider, new AgentOptions { Verbose = false });
        var socket = new RecordingSocket();
        var queue = NewQueue();

        await queue.EnqueueAsync(Request("s-1", "first", dragon, socket));
        await queue.EnqueueAsync(Request("s-1", "second", dragon, socket));

        await WaitUntil(() => socket.Replies().Count == 2);
        socket.Replies().Should().Equal("reply to first", "reply to second");
        provider.MaxConcurrent.Should().Be(1, "one Dragon answers one message at a time");
        provider.UserTurnsPerCall.Last().Should().Equal(["first", "second"], "the second answer sees the first exchange");
    }

    [Fact]
    public async Task Other_sessions_are_not_held_up_by_a_busy_one()
    {
        var provider = new SlowProvider(TimeSpan.FromMilliseconds(400));
        var busy = new DragonAgent(provider, new AgentOptions { Verbose = false });
        var other = new DragonAgent(provider, new AgentOptions { Verbose = false });
        var busySocket = new RecordingSocket();
        var otherSocket = new RecordingSocket();
        var queue = NewQueue();

        await queue.EnqueueAsync(Request("busy", "one", busy, busySocket));
        await queue.EnqueueAsync(Request("busy", "two", busy, busySocket));
        await queue.EnqueueAsync(Request("other", "hello", other, otherSocket));

        await WaitUntil(() => otherSocket.Replies().Count == 1);
        busySocket.Replies().Should().HaveCountLessThan(2, "the other session is answered while the busy one still works");
        await WaitUntil(() => busySocket.Replies().Count == 2);
        busySocket.Replies().Should().Equal("reply to one", "reply to two");
    }

    [Fact]
    public async Task The_cancel_command_drops_a_waiting_message_and_the_running_one_still_finishes()
    {
        var provider = new SlowProvider(TimeSpan.FromMilliseconds(400));
        var dragon = new DragonAgent(provider, new AgentOptions { Verbose = false });
        var socket = new RecordingSocket();
        var queue = NewQueue();

        await queue.EnqueueAsync(Request("s-1", "first", dragon, socket));
        await queue.EnqueueAsync(Request("s-1", "second", dragon, socket));
        await WaitUntil(() => provider.UserTurnsPerCall.Count == 1);

        (await queue.CancelSessionRequestAsync("s-1")).Should().BeTrue();

        (await queue.WaitForSessionIdleAsync("s-1", TimeSpan.FromSeconds(10))).Should().BeTrue();
        socket.Replies().Should().Equal("reply to first");
        provider.UserTurnsPerCall.Should().HaveCount(1);
    }

    // --- Dispose (TASK-181) -------------------------------------------------------------------------

    [Fact]
    public void Disposing_an_idle_queue_stops_its_workers_without_throwing()
    {
        var queue = NewQueue();

        var dispose = () => queue.Dispose();

        dispose.Should().NotThrow();
        dispose.Should().NotThrow("a second Dispose does nothing");
    }

    [Fact]
    public async Task Disposing_with_a_request_running_does_not_throw_and_its_reply_is_not_sent_afterwards()
    {
        var provider = new SlowProvider(TimeSpan.FromMilliseconds(800));
        var dragon = new DragonAgent(provider, new AgentOptions { Verbose = false });
        var socket = new RecordingSocket();
        var queue = NewQueue();
        await queue.EnqueueAsync(Request("s-1", "first", dragon, socket));
        await WaitUntil(() => provider.UserTurnsPerCall.Count == 1);
        var sentBefore = socket.Sent.Count;

        var started = DateTime.UtcNow;
        var dispose = () => queue.Dispose();

        dispose.Should().NotThrow();
        (DateTime.UtcNow - started).Should().BeLessThan(TimeSpan.FromSeconds(2), "shutdown does not wait out a running answer");
        await Task.Delay(1500); // the model call ends after the queue is gone
        socket.Replies().Should().BeEmpty("nothing is sent on a socket after its queue was disposed");
        lock (socket.Sent) socket.Sent.Count.Should().Be(sentBefore);
    }
}
