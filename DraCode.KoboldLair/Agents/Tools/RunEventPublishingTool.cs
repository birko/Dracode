using System.Text.Json;
using Birko.AI.Tools;
using DraCode.KoboldLair.Events.Run;

namespace DraCode.KoboldLair.Agents.Tools
{
    /// <summary>
    /// Wraps a tool so each call publishes <see cref="ToolCallStartedEvent"/> and <see cref="ToolCallResultEvent"/>. Used by a
    /// Kobold run that goes through <c>Agent.RunAsync</c>, whose loop has no structured tool-call hook (TASK-106); the
    /// enhanced plan path publishes these from its own loop.
    /// </summary>
    public sealed class RunEventPublishingTool : Tool
    {
        private readonly Action<KoboldRunEvent> _publish;

        public RunEventPublishingTool(Tool inner, Action<KoboldRunEvent> publish)
        {
            Inner = inner;
            _publish = publish;
        }

        /// <summary>The wrapped tool.</summary>
        public Tool Inner { get; }

        public override string Name => Inner.Name;
        public override string Description => Inner.Description;
        public override object? InputSchema => Inner.InputSchema;

        public override async Task<string> ExecuteAsync(string workingDirectory, Dictionary<string, object> input,
            CancellationToken cancellationToken = default)
        {
            _publish(new ToolCallStartedEvent { ToolName = Name, InputJson = JsonSerializer.Serialize(input) });
            var result = await Inner.ExecuteAsync(workingDirectory, input, cancellationToken);
            var preview = result.Length > 500 ? string.Concat(result.AsSpan(0, 500), "...") : result;
            _publish(new ToolCallResultEvent { ToolName = Name, ResultPreview = preview });
            return result;
        }
    }
}
