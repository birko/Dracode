using System.Collections.Concurrent;
using System.Threading.Channels;
using DraCode.KoboldLair.Events.Run;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// In-memory, per-run publish/subscribe for Kobold execution telemetry (TASK-037). Each run
    /// (<c>runId</c>) fans out to zero or more subscriber channels. Publication is synchronous and
    /// non-blocking — a slow or absent subscriber never stalls the Kobold hot path:
    /// <list type="bullet">
    ///   <item>No subscribers ⇒ publish is a no-op (events dropped).</item>
    ///   <item>Each subscriber owns a bounded channel with <see cref="BoundedChannelFullMode.DropOldest"/>,
    ///         so a slow consumer loses old events instead of back-pressuring the producer.</item>
    ///   <item>Subscribers attach mid-run and receive only subsequent events (no replay — events aren't persisted).</item>
    /// </list>
    /// This is deliberately NOT <c>Birko.EventBus</c> (which awaits handlers sequentially and keys by event
    /// type) — those semantics would block execution and can't drop/attach-mid-run. The WS (TASK-038) and
    /// SSE (TASK-045) transports consume this.
    /// </summary>
    public sealed class KoboldRunEventSource
    {
        private const int DefaultCapacity = 256;

        private sealed class RunChannels
        {
            public readonly object Gate = new();
            public readonly List<Channel<KoboldRunEvent>> Subscribers = new();
            public int Sequence;
        }

        private readonly ConcurrentDictionary<Guid, RunChannels> _runs = new();
        private readonly int _capacity;

        public KoboldRunEventSource(int capacity = DefaultCapacity) => _capacity = capacity;

        /// <summary>
        /// Publishes an event to every current subscriber of its <see cref="KoboldRunEvent.RunId"/>.
        /// Synchronous, never throws, never blocks. Stamps a monotonic <see cref="KoboldRunEvent.Sequence"/>.
        /// </summary>
        public void Publish(KoboldRunEvent evt)
        {
            if (!_runs.TryGetValue(evt.RunId, out var run))
                return; // no run registered / no subscribers ever attached → drop

            Channel<KoboldRunEvent>[] targets;
            int seq;
            lock (run.Gate)
            {
                if (run.Subscribers.Count == 0) return; // attached then all left → drop
                seq = ++run.Sequence;
                targets = run.Subscribers.ToArray();
            }

            var stamped = evt with { Sequence = seq };
            foreach (var ch in targets)
            {
                try { ch.Writer.TryWrite(stamped); } // DropOldest bounds memory; TryWrite never blocks
                catch { /* writer completed/disposed mid-publish — ignore */ }
            }
        }

        /// <summary>
        /// Attaches a subscriber to <paramref name="runId"/>. Returns a reader for subsequent events and an
        /// <see cref="IDisposable"/> that detaches it. Calling before the run starts is fine — the run entry
        /// is created on demand.
        /// </summary>
        public IDisposable Subscribe(Guid runId, out ChannelReader<KoboldRunEvent> reader)
        {
            var run = _runs.GetOrAdd(runId, _ => new RunChannels());
            var channel = Channel.CreateBounded<KoboldRunEvent>(new BoundedChannelOptions(_capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
            lock (run.Gate) run.Subscribers.Add(channel);
            reader = channel.Reader;
            return new Subscription(run, channel);
        }

        /// <summary>
        /// Completes all subscriber readers for the run and drops it. Call from the Kobold's <c>finally</c>
        /// so abandoned runs don't leak channels.
        /// </summary>
        public void CompleteRun(Guid runId)
        {
            if (!_runs.TryRemove(runId, out var run)) return;
            lock (run.Gate)
            {
                foreach (var ch in run.Subscribers) ch.Writer.TryComplete();
                run.Subscribers.Clear();
            }
        }

        private sealed class Subscription : IDisposable
        {
            private readonly RunChannels _run;
            private readonly Channel<KoboldRunEvent> _channel;
            private bool _disposed;

            public Subscription(RunChannels run, Channel<KoboldRunEvent> channel)
            {
                _run = run;
                _channel = channel;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                lock (_run.Gate) _run.Subscribers.Remove(_channel);
                _channel.Writer.TryComplete();
            }
        }
    }
}
