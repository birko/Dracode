using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// Every task status survives the markdown task file (TASK-166). The markdown is read only when a task file has no JSON
/// sidecar (an older project, or a lost sidecar), and its reader knew six status words: a Failed or BlockedByFailure task
/// came back Unassigned, and Drake offered it as new work. A task carrying an error message was written as "error",
/// which reads as Done, whatever its status.
/// </summary>
public class TaskMarkdownRoundTripTests
{
    private static TaskRecord RoundTrip(TaskStatus status, string? errorMessage = null)
    {
        var tracker = new TaskTracker();
        var task = tracker.AddTask("[api-1] Build the store");
        task.Status = status;
        task.ErrorMessage = errorMessage;
        var reloaded = new TaskTracker();
        reloaded.LoadFromMarkdown(tracker.GenerateMarkdown()).Should().Be(1);
        return reloaded.GetTaskById(task.Id)!;
    }

    [Fact]
    public void A_failed_task_and_a_task_blocked_by_a_failure_keep_their_status()
    {
        RoundTrip(TaskStatus.Failed, "build broke").Status.Should().Be(TaskStatus.Failed);
        RoundTrip(TaskStatus.BlockedByFailure).Status.Should().Be(TaskStatus.BlockedByFailure);
        RoundTrip(TaskStatus.BlockedByFailure, "dependency api-0 failed").Status.Should().Be(TaskStatus.BlockedByFailure);
    }

    public static TheoryData<TaskStatus> AllStatuses()
    {
        var data = new TheoryData<TaskStatus>();
        foreach (var status in Enum.GetValues<TaskStatus>())
            data.Add(status);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Every_status_round_trips_through_the_markdown_file(TaskStatus status)
    {
        RoundTrip(status).Status.Should().Be(status);
    }

    [Theory]
    [MemberData(nameof(AllStatuses))]
    public void Every_status_round_trips_with_an_error_message_too(TaskStatus status)
    {
        // "error" is written only for a finished task with an error message, and reads back as Done (by design)
        var reloaded = RoundTrip(status, "something went wrong");
        reloaded.Status.Should().Be(status);
    }

    [Fact]
    public void A_done_task_with_an_error_message_still_reads_back_as_done_with_an_error()
    {
        var reloaded = RoundTrip(TaskStatus.Done, "finished with warnings");
        reloaded.Status.Should().Be(TaskStatus.Done);
        reloaded.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
