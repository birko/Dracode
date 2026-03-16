using Birko.EventBus;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.Events.Handlers
{
    /// <summary>
    /// Handles task status change events by logging them.
    /// Additional logic (notifications, metrics, etc.) can be added here.
    /// </summary>
    public class TaskStatusChangedHandler : IEventHandler<TaskStatusChangedEvent>
    {
        private readonly ILogger<TaskStatusChangedHandler> _logger;

        public TaskStatusChangedHandler(ILogger<TaskStatusChangedHandler> logger)
        {
            _logger = logger;
        }

        public Task HandleAsync(TaskStatusChangedEvent @event, EventContext context, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "Task status changed: {OldStatus} -> {NewStatus} | Project: {ProjectId}, Task: {TaskId}{Error}",
                @event.OldStatus,
                @event.NewStatus,
                @event.ProjectId,
                @event.TaskId.Length > 8 ? @event.TaskId[..8] : @event.TaskId,
                string.IsNullOrEmpty(@event.ErrorMessage) ? "" : $", Error: {@event.ErrorMessage}");

            return Task.CompletedTask;
        }
    }
}
