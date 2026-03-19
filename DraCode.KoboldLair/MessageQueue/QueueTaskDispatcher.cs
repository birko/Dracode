using Birko.MessageQueue;
using DraCode.KoboldLair.Messages;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.MessageQueue;

/// <summary>
/// Dispatches task assignments via a message queue for distributed Kobold execution.
/// Drake publishes assignments; KoboldWorkerService consumes them.
/// </summary>
public class QueueTaskDispatcher : ITaskDispatcher
{
    private readonly IMessageProducer _producer;
    private readonly ILogger<QueueTaskDispatcher>? _logger;
    private readonly string _assignmentQueue;

    public bool IsDistributed => true;

    public QueueTaskDispatcher(
        IMessageProducer producer,
        string assignmentQueue = "kobold.tasks.assign",
        ILogger<QueueTaskDispatcher>? logger = null)
    {
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        _assignmentQueue = assignmentQueue;
        _logger = logger;
    }

    public async Task DispatchTaskAsync(TaskAssignmentMessage assignment, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation(
            "Dispatching task {TaskId} ({AgentType}) for project {ProjectId} via message queue",
            assignment.TaskId, assignment.AgentType, assignment.ProjectId);

        var headers = new MessageHeaders
        {
            CorrelationId = assignment.CorrelationId,
            Custom =
            {
                ["projectId"] = assignment.ProjectId,
                ["taskId"] = assignment.TaskId,
                ["agentType"] = assignment.AgentType
            }
        };

        await _producer.SendAsync(_assignmentQueue, assignment, headers, cancellationToken);

        _logger?.LogDebug(
            "Task {TaskId} dispatched to queue {Queue} with correlation {CorrelationId}",
            assignment.TaskId, _assignmentQueue, assignment.CorrelationId);
    }
}
