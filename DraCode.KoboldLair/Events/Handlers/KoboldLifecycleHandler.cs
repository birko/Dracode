using Birko.EventBus;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Events.Handlers
{
    /// <summary>
    /// Handles Kobold lifecycle events by logging them.
    /// Additional logic (metrics, alerts, etc.) can be added here.
    /// </summary>
    public class KoboldLifecycleHandler : IEventHandler<KoboldLifecycleEvent>
    {
        private readonly ILogger<KoboldLifecycleHandler> _logger;

        public KoboldLifecycleHandler(ILogger<KoboldLifecycleHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(KoboldLifecycleEvent @event, EventContext context, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Kobold lifecycle: {Action} | Project: {ProjectId}, Task: {TaskId}, Agent: {AgentType}, Kobold: {KoboldId}",
                @event.Action,
                @event.ProjectId,
                @event.TaskId.Length > 8 ? @event.TaskId[..8] : @event.TaskId,
                @event.AgentType,
                @event.KoboldId.ToString()[..8]);

            return Task.CompletedTask;
        }
    }
}
