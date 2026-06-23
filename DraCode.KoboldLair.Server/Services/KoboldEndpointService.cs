using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using DraCode.KoboldLair.Events.Run;
using DraCode.KoboldLair.Server.Models.WebSocket;
using DraCode.KoboldLair.Services;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Server.Services
{
    /// <summary>
    /// Hosts the <c>/kobold</c> WebSocket protocol (TASK-038): parse the initial mode payload, dispatch
    /// to the matching <see cref="IKoboldRunModeHandler"/>, subscribe to <see cref="KoboldRunEventSource"/>,
    /// and stream the run telemetry to the client as <c>kobold_*</c> wire frames. The mode handlers (ad-hoc /
    /// project) own how a Kobold is actually summoned (TASK-039 / TASK-040); this service only owns the
    /// transport + protocol.
    /// </summary>
    public sealed class KoboldEndpointService
    {
        private readonly KoboldRunEventSource _eventSource;
        private readonly RunRegistry _runRegistry;
        private readonly IReadOnlyList<IKoboldRunModeHandler> _handlers;
        private readonly ILogger<KoboldEndpointService> _logger;

        private static readonly JsonSerializerOptions s_read = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions s_wire = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };

        public KoboldEndpointService(
            KoboldRunEventSource eventSource,
            RunRegistry runRegistry,
            IEnumerable<IKoboldRunModeHandler> handlers,
            ILogger<KoboldEndpointService> logger)
        {
            _eventSource = eventSource;
            _runRegistry = runRegistry;
            _handlers = handlers.ToList();
            _logger = logger;
        }

        /// <summary>
        /// Drives one accepted <c>/kobold</c> connection end-to-end. Receives the initial payload, dispatches
        /// by mode, then relays the run-event stream until the run terminates or the client disconnects.
        /// </summary>
        public async Task HandleWebSocketAsync(WebSocket webSocket, KoboldCaller caller, CancellationToken ct = default)
        {
            using var sender = new WebSocketSender(webSocket, _logger);

            KoboldRunRequest? request;
            try
            {
                request = await ReceiveInitialRequestAsync(webSocket, ct);
            }
            catch (Exception ex)
            {
                await SendAsync(sender, KoboldWireMessage.Error(Guid.Empty, $"invalid initial payload: {ex.Message}"), ct);
                return;
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Mode))
            {
                await SendAsync(sender, KoboldWireMessage.Error(Guid.Empty, "missing 'mode' in initial payload"), ct);
                return;
            }

            var handler = _handlers.FirstOrDefault(h => string.Equals(h.Mode, request.Mode, StringComparison.OrdinalIgnoreCase));
            if (handler is null)
            {
                await SendAsync(sender, KoboldWireMessage.Error(Guid.Empty, $"unknown mode '{request.Mode}'"), ct);
                return;
            }

            // Own the runId and subscribe BEFORE starting the run, so no early events are dropped.
            var runId = Guid.NewGuid();
            using var subscription = _eventSource.Subscribe(runId, out var reader);

            // Track the run in the shared registry so it's queryable via GET /api/v1/runs/{id} too — same
            // engine, same status store across transports (TASK-044). Register before start (subscribe-before-start).
            _runRegistry.Register(runId, caller.Sub, handler.Mode);

            KoboldRunStartInfo startInfo;
            try
            {
                startInfo = await handler.StartAsync(request, runId, caller, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "/kobold {Mode} run {RunId} failed to start", request.Mode, runId);
                _runRegistry.Fail(runId, ex.Message);
                await SendAsync(sender, KoboldWireMessage.Error(runId, ex.Message), ct);
                return;
            }

            // A client disconnect cancels relaying only — the Kobold keeps running (criterion 4).
            using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var watchTask = WatchForCloseAsync(webSocket, pumpCts, ct);
            try
            {
                await PumpAsync(runId, startInfo, reader, (msg, c) => SendAsync(sender, msg, c), pumpCts.Token);
            }
            finally
            {
                pumpCts.Cancel();
                try { await watchTask; } catch { /* watch loop already faulted/cancelled */ }
            }
        }

        /// <summary>
        /// Transport-agnostic protocol core (directly unit-testable): emits <c>kobold_run_started</c>, then
        /// translates and forwards every run event via <paramref name="send"/>, stopping on a terminal event
        /// (<see cref="RunCompletedEvent"/> / <see cref="RunErrorEvent"/>) or when the reader completes.
        /// </summary>
        public static async Task PumpAsync(
            Guid runId,
            KoboldRunStartInfo startInfo,
            ChannelReader<KoboldRunEvent> reader,
            Func<KoboldWireMessage, CancellationToken, Task> send,
            CancellationToken ct)
        {
            await send(KoboldWireMessage.RunStarted(runId, startInfo.Mode, startInfo.Worktree), ct);

            try
            {
                await foreach (var evt in reader.ReadAllAsync(ct))
                {
                    await send(KoboldWireMessage.From(evt), ct);
                    if (evt is RunCompletedEvent or RunErrorEvent)
                        break; // terminal — stop relaying
                }
            }
            catch (OperationCanceledException)
            {
                // Client disconnected or server shutting down — stop relaying without faulting.
            }
        }

        private static async Task<KoboldRunRequest?> ReceiveInitialRequestAsync(WebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[8192];
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                    return null;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var text = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            return JsonSerializer.Deserialize<KoboldRunRequest>(text, s_read);
        }

        private static async Task WatchForCloseAsync(WebSocket ws, CancellationTokenSource pumpCts, CancellationToken ct)
        {
            var buffer = new byte[1024];
            try
            {
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        break;
                }
            }
            catch
            {
                // Socket faulted → treat as a disconnect.
            }
            finally
            {
                pumpCts.Cancel();
            }
        }

        private static Task SendAsync(WebSocketSender sender, KoboldWireMessage msg, CancellationToken ct)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(msg, s_wire);
            return sender.SendAsync(bytes, ct);
        }
    }
}
