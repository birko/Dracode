using DraCode.KoboldLair.Messages;
using Microsoft.Extensions.Logging;

namespace DraCode.KoboldLair.MessageQueue;

/// <summary>
/// Direct in-process task dispatcher that preserves the current behavior.
/// Tasks are dispatched via a callback to the Drake's existing SummonKoboldAsync method.
/// This is the default dispatcher when message queue is not enabled.
/// </summary>
public class DirectTaskDispatcher : ITaskDispatcher
{
    private readonly Func<TaskAssignmentMessage, CancellationToken, Task>? _dispatchCallback;
    private readonly ILogger<DirectTaskDispatcher>? _logger;

    public bool IsDistributed => false;

    /// <summary>
    /// Creates a direct dispatcher with a callback to the in-process Kobold creation logic.
    /// The callback is typically wired to Drake.SummonKoboldAsync equivalent.
    /// </summary>
    public DirectTaskDispatcher(
        Func<TaskAssignmentMessage, CancellationToken, Task>? dispatchCallback = null,
        ILogger<DirectTaskDispatcher>? logger = null)
    {
        _dispatchCallback = dispatchCallback;
        _logger = logger;
    }

    public async Task DispatchTaskAsync(TaskAssignmentMessage assignment, CancellationToken cancellationToken = default)
    {
        _logger?.LogDebug(
            "Dispatching task {TaskId} ({AgentType}) for project {ProjectId} in-process",
            assignment.TaskId, assignment.AgentType, assignment.ProjectId);

        if (_dispatchCallback != null)
        {
            await _dispatchCallback(assignment, cancellationToken);
        }
        else
        {
            _logger?.LogWarning("DirectTaskDispatcher has no callback configured — task {TaskId} was not dispatched", assignment.TaskId);
        }
    }
}
