using DraCode.KoboldLair.Messages;

namespace DraCode.KoboldLair.MessageQueue;

/// <summary>
/// Abstraction for dispatching task assignments to Kobold workers.
/// Decouples Drake from the Kobold creation mechanism.
/// </summary>
public interface ITaskDispatcher
{
    /// <summary>
    /// Dispatches a task assignment for execution by a Kobold worker.
    /// In direct mode, this creates and runs a Kobold in-process.
    /// In queue mode, this publishes a message to the task queue.
    /// </summary>
    Task DispatchTaskAsync(TaskAssignmentMessage assignment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether this dispatcher uses a message queue (vs direct in-process execution).
    /// </summary>
    bool IsDistributed { get; }
}
