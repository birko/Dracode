using Birko.EventBus;
using Birko.MessageQueue;
using DraCode.KoboldLair.Events;
using DraCode.KoboldLair.Messages;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.MessageQueue;

/// <summary>
/// Handles task completion messages from distributed Kobold workers.
/// Updates task status and publishes events for existing handlers.
/// </summary>
public class TaskCompletionHandler : IMessageHandler<TaskCompletionMessage>
{
    private readonly ILogger<TaskCompletionHandler>? _logger;
    private readonly IEventBus? _eventBus;
    private readonly Action<TaskCompletionMessage>? _onTaskCompleted;

    public TaskCompletionHandler(
        ILogger<TaskCompletionHandler>? logger = null,
        IEventBus? eventBus = null,
        Action<TaskCompletionMessage>? onTaskCompleted = null)
    {
        _logger = logger;
        _eventBus = eventBus;
        _onTaskCompleted = onTaskCompleted;
    }

    public async Task HandleAsync(TaskCompletionMessage message, MessageContext context, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation(
            "Task {TaskId} completed: {Status} ({AgentType}, {Duration}ms, {Iterations} iterations)",
            message.TaskId,
            message.Success ? "Success" : "Failed",
            message.AgentType,
            message.Duration.TotalMilliseconds,
            message.IterationsUsed);

        // Publish lifecycle event for existing handlers
        if (_eventBus != null)
        {
            var koboldId = Guid.TryParse(message.KoboldId, out var parsed) ? parsed : Guid.NewGuid();
            var lifecycleEvent = new KoboldLifecycleEvent
            {
                ProjectId = message.ProjectId,
                TaskId = message.TaskId,
                AgentType = message.AgentType,
                KoboldId = koboldId,
                Action = message.Success ? KoboldLifecycleAction.Completed : KoboldLifecycleAction.Failed
            };

            try
            {
                await _eventBus.PublishAsync(lifecycleEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to publish lifecycle event for task {TaskId}", message.TaskId);
            }
        }

        // Invoke completion callback for Drake to handle post-task logic
        _onTaskCompleted?.Invoke(message);
    }
}
