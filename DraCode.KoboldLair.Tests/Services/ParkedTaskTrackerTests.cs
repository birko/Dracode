using DraCode.KoboldLair.Models.Tasks;
using FluentAssertions;
using TaskStatus = DraCode.KoboldLair.Models.Tasks.TaskStatus;

namespace DraCode.KoboldLair.Tests.Services;

/// <summary>
/// A parked task (<see cref="TaskStatus.AwaitingHumanDecision"/>) keeps its status through the task tracker's
/// markdown and JSON files (TASK-019). An unknown status word reads as Unassigned, which would re-run a parked task.
/// </summary>
public class ParkedTaskTrackerTests
{
    private static (TaskTracker tracker, TaskRecord task) TrackerWithParkedTask()
    {
        var tracker = new TaskTracker();
        var task = tracker.AddTask("Decide the storage engine");
        tracker.UpdateTask(task, TaskStatus.AwaitingHumanDecision, "csharp");
        return (tracker, task);
    }

    [Fact]
    public void A_parked_task_survives_the_markdown_round_trip()
    {
        var (tracker, task) = TrackerWithParkedTask();

        var reloaded = new TaskTracker();
        reloaded.LoadFromMarkdown(tracker.GenerateMarkdown());

        reloaded.GetTaskById(task.Id)!.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
    }

    [Fact]
    public void A_parked_task_survives_the_json_round_trip()
    {
        var (tracker, task) = TrackerWithParkedTask();
        var path = Path.Combine(Path.GetTempPath(), $"tracker_{Guid.NewGuid():N}.json");
        try
        {
            tracker.SaveToJsonFile(path);
            var reloaded = new TaskTracker();
            reloaded.LoadFromJsonFile(path);

            reloaded.GetTaskById(task.Id)!.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void A_parked_task_with_an_error_message_does_not_read_back_as_done()
    {
        var (tracker, task) = TrackerWithParkedTask();
        tracker.SetError(task, "Escalated: needs split");
        tracker.UpdateTask(task, TaskStatus.AwaitingHumanDecision);

        var reloaded = new TaskTracker();
        reloaded.LoadFromMarkdown(tracker.GenerateMarkdown());

        reloaded.GetTaskById(task.Id)!.Status.Should().Be(TaskStatus.AwaitingHumanDecision);
    }

    [Fact]
    public void The_markdown_summary_counts_parked_tasks()
    {
        var (tracker, _) = TrackerWithParkedTask();

        tracker.GenerateMarkdown().Should().Contain("**Awaiting Human Decision**: 1");
    }
}
