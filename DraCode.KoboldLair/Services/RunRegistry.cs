using System.Collections.Concurrent;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Models.Agents;

namespace DraCode.KoboldLair.Services
{
    /// <summary>
    /// In-memory directory of Kobold runs, keyed by <c>runId</c>, that survives run completion so a
    /// poll-based client can ask "is it done?" after the fact (TASK-044). The <see cref="KoboldRunEventSource"/>
    /// is live-only — it drops events with no subscriber and forgets a run on <c>CompleteRun</c> — so this
    /// registry attaches a persistent per-run subscriber at <see cref="Register"/> time (before the run starts)
    /// and folds the event stream down to a single queryable <see cref="RunRecord"/>. Both transports register
    /// here (the <c>/kobold</c> WebSocket and the <c>POST /api/v1/runs</c> REST endpoint) — one run engine,
    /// one status store — and TASK-045's SSE / TASK-046's agents view read from it.
    /// </summary>
    public sealed class RunRegistry
    {
        /// <summary>Coarse, transport-facing run state (the REST <c>status</c> field).</summary>
        public enum RunState { Pending, Running, Completed, Failed }

        /// <summary>A single run's metadata + latest folded status. Mutated in place by the reader loop.</summary>
        public sealed class RunRecord
        {
            public required Guid RunId { get; init; }
            /// <summary>Owning caller (JWT <c>sub</c>); null when started without an identity. Gate reads on this.</summary>
            public string? Owner { get; init; }
            public required string Mode { get; init; }
            public required DateTime StartedAt { get; init; }
            public RunState State { get; set; } = RunState.Pending;
            public DateTime? CompletedAt { get; set; }
            public int CompletedSteps { get; set; }
            public int TotalSteps { get; set; }
            public string? Summary { get; set; }

            public bool IsTerminal => State is RunState.Completed or RunState.Failed;

            /// <summary>A consistent point-in-time copy (taken under the registry lock).</summary>
            internal RunRecord Snapshot() => new()
            {
                RunId = RunId, Owner = Owner, Mode = Mode, StartedAt = StartedAt,
                State = State, CompletedAt = CompletedAt,
                CompletedSteps = CompletedSteps, TotalSteps = TotalSteps, Summary = Summary
            };
        }

        private readonly KoboldRunEventSource _events;
        private readonly int _maxRuns;
        private readonly ConcurrentDictionary<Guid, RunRecord> _runs = new();
        /// <summary>Guards multi-field reads/writes of a <see cref="RunRecord"/> so callers never see a torn set.</summary>
        private readonly object _gate = new();

        public RunRegistry(KoboldRunEventSource events, int maxRuns = 1000)
        {
            _events = events;
            _maxRuns = maxRuns;
        }

        /// <summary>
        /// Records a new run as <see cref="RunState.Pending"/> and subscribes to its event stream so the
        /// record tracks status to completion. MUST be called before the run starts (the event source has no
        /// replay), mirroring the WS endpoint's subscribe-before-start contract. Returns the live record.
        /// </summary>
        public RunRecord Register(Guid runId, string? owner, string mode)
        {
            var record = new RunRecord
            {
                RunId = runId,
                Owner = owner,
                Mode = mode,
                StartedAt = DateTime.UtcNow
            };
            _runs[runId] = record;
            PruneIfNeeded();

            var subscription = _events.Subscribe(runId, out var reader);
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var evt in reader.ReadAllAsync())
                        lock (_gate) Apply(record, evt);
                }
                catch { /* reader faulted/cancelled — leave the last known state */ }
                finally { subscription.Dispose(); }
            });

            return record;
        }

        /// <summary>Folds one run event into the record's state. Terminal is monotonic — once a run has
        /// completed/failed, later (out-of-order or duplicate) events can't resurrect or flip it.</summary>
        private static void Apply(RunRecord record, KoboldRunEvent evt)
        {
            if (record.IsTerminal) return;
            switch (evt)
            {
                case RunCompletedEvent c:
                    record.State = c.FinalStatus == KoboldStatus.Done ? RunState.Completed : RunState.Failed;
                    record.CompletedSteps = c.CompletedSteps;
                    record.TotalSteps = c.TotalSteps;
                    record.Summary = c.FinalStatus.ToString();
                    record.CompletedAt = DateTime.UtcNow;
                    break;
                case RunErrorEvent e:
                    record.State = RunState.Failed;
                    record.Summary = e.Message;
                    record.CompletedAt = DateTime.UtcNow;
                    break;
                case PlanStepUpdatedEvent p:
                    record.State = RunState.Running;
                    record.CompletedSteps = p.CompletedSteps;
                    record.TotalSteps = p.TotalSteps;
                    break;
                default:
                    // tool-call / reflection / anything else → the run is alive (terminal already returned above)
                    record.State = RunState.Running;
                    break;
            }
        }

        /// <summary>
        /// Marks a run failed when its start threw before the Kobold could emit any event (so no terminal
        /// event / <c>CompleteRun</c> is coming). Also completes the run on the event source to release the
        /// registry's reader. No-op if the run already reached a terminal state.
        /// </summary>
        public void Fail(Guid runId, string message)
        {
            if (_runs.TryGetValue(runId, out var record))
            {
                lock (_gate)
                {
                    if (!record.IsTerminal)
                    {
                        record.State = RunState.Failed;
                        record.Summary = message;
                        record.CompletedAt = DateTime.UtcNow;
                    }
                }
            }
            _events.CompleteRun(runId);
        }

        /// <summary>A consistent snapshot of the run, or null if unknown.</summary>
        public RunRecord? Get(Guid runId)
        {
            if (!_runs.TryGetValue(runId, out var r)) return null;
            lock (_gate) return r.Snapshot();
        }

        /// <summary>Consistent snapshots of all runs owned by <paramref name="owner"/> (null matches null-owner runs).</summary>
        public IReadOnlyList<RunRecord> ListByOwner(string? owner)
        {
            lock (_gate)
                return _runs.Values.Where(r => r.Owner == owner)
                    .OrderByDescending(r => r.StartedAt).Select(r => r.Snapshot()).ToList();
        }

        /// <summary>Consistent snapshots of all runs (admin view).</summary>
        public IReadOnlyList<RunRecord> List()
        {
            lock (_gate)
                return _runs.Values.OrderByDescending(r => r.StartedAt).Select(r => r.Snapshot()).ToList();
        }

        /// <summary>Bounds memory: when over capacity, evict the oldest terminal runs first.</summary>
        private void PruneIfNeeded()
        {
            if (_runs.Count <= _maxRuns) return;
            lock (_gate)
            {
                foreach (var stale in _runs.Values
                             .Where(r => r.IsTerminal)
                             .OrderBy(r => r.CompletedAt ?? r.StartedAt)
                             .Take(_runs.Count - _maxRuns))
                {
                    _runs.TryRemove(stale.RunId, out _);
                }
            }
        }
    }
}
