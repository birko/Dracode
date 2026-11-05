using System.Text.Json;
using Birko.AI.Orchestration.Models;
using DraCode.KoboldLair.Data.Repositories.Sql;
using DraCode.KoboldLair.Models.Agents;
using DraCode.KoboldLair.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace DraCode.KoboldLair.Tests.Data;

/// <summary>
/// TASK-126: plans stored by DraCode's own plan types load into Birko's. The fixtures were written by the pre-migration
/// code (commit fe6d687): <c>*.plan-data.json</c> is the <c>plans.PlanDataJson</c> column as <c>SqlPlanRepository</c>
/// stored it, <c>*-plan.json</c> the plan file <c>KoboldPlanService</c> wrote — three plans for the flappy-dragon
/// project's chasecam, collision and world tasks.
/// </summary>
public class LegacyPlanLoadTests : IAsyncLifetime
{
    private const string KoboldA = "3f2c9a71-5b8e-4d0a-9c61-2e7f4b1d8a05";
    private const string KoboldB = "b81e4c27-0d93-4f6a-a2b5-91c3e6d7f402";

    private static readonly JsonSerializerOptions PlanFileOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"dracode_legacyplans_{Guid.NewGuid():N}.db");

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
        return Task.CompletedTask;
    }

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LegacyPlans", name));

    /// <summary>Puts a stored legacy row into a fresh database and reads it back with the repository's own loader.</summary>
    private async Task<KoboldImplementationPlan> LoadStoredAsync(string name)
    {
        var legacyJson = Fixture($"{name}.plan-data.json");
        using var doc = JsonDocument.Parse(legacyJson);
        var projectId = doc.RootElement.GetProperty("projectId").GetString()!;
        var taskId = doc.RootElement.GetProperty("taskId").GetString()!;

        var repo = new SqlPlanRepository(_dbPath);
        await repo.InitializeAsync();
        await repo.SavePlanAsync(new KoboldImplementationPlan { ProjectId = projectId, TaskId = taskId });

        using (var conn = new SqliteConnection($"Data Source={_dbPath};Pooling=False"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE plans SET PlanDataJson = $json WHERE TaskId = $task";
            cmd.Parameters.AddWithValue("$json", legacyJson);
            cmd.Parameters.AddWithValue("$task", taskId);
            cmd.ExecuteNonQuery().Should().Be(1);
        }

        var plan = await repo.LoadPlanAsync(projectId, taskId);
        plan.Should().NotBeNull($"the stored {name} plan must load");
        return plan!;
    }

    private static void ShouldBeChasecam(KoboldImplementationPlan plan)
    {
        plan.PlanFilename.Should().Be("spring-damper-chasecam");
        plan.FeatureId.Should().Be("FEATURE-001");
        plan.FeatureName.Should().Be("Flight and camera");
        plan.SpecificationVersion.Should().Be(2);
        plan.Status.Should().Be(PlanStatus.Failed);
        plan.CurrentStepIndex.Should().Be(1);
        plan.Steps.Select(s => s.Status).Should().Equal(StepStatus.Completed, StepStatus.Failed, StepStatus.Skipped);

        var retried = plan.Steps[0];
        retried.ErrorMessage.Should().StartWith("Error: LLM request failed: 503");
        retried.ErrorCategory.Should().Be(Birko.AI.Models.ErrorCategory.Transient);
        retried.RetryCount.Should().Be(1);
        retried.Metrics.IterationsUsed.Should().Be(12);

        plan.Steps[1].ErrorMessage.Should().Contain("TS2339");
        plan.Steps[1].ErrorCategory.Should().Be(Birko.AI.Models.ErrorCategory.Permanent);
        plan.Steps[2].ErrorCategory.Should().BeNull();

        plan.Reflections.Should().HaveCount(2);
        plan.Reflections[1].Decision.Should().Be(ReflectionDecision.Escalate);
        plan.Reflections[1].EscalationType.Should().Be(EscalationType.WrongApproach);

        var escalation = plan.Escalations.Should().ContainSingle().Subject;
        escalation.WorkerId.Should().Be(KoboldA);
        escalation.Source.Should().Be(EscalationSource.ReflectionTool);
        escalation.Status.Should().Be(EscalationStatus.Resolved);
        escalation.ReflectionHistory.Should().ContainSingle();
    }

    [Fact]
    public async Task A_stored_plan_with_retried_and_failed_steps_and_an_escalation_loads_intact()
        => ShouldBeChasecam(await LoadStoredAsync("chasecam"));

    [Fact]
    public async Task An_Unknown_error_category_loads_as_unclassified_and_assignments_keep_their_Kobold_keys()
    {
        var plan = await LoadStoredAsync("collision");

        plan.Status.Should().Be(PlanStatus.InProgress);
        plan.Steps[1].Status.Should().Be(StepStatus.Failed);
        plan.Steps[1].ErrorMessage.Should().Be("The agent reported the step could not be completed");
        plan.Steps[1].ErrorCategory.Should().BeNull("DraCode's \"Unknown\" has no Birko counterpart: not classified");
        plan.Steps[0].Metrics.AutoCompleted.Should().BeTrue();
        plan.AssignedStepIndices.Should().BeEquivalentTo(new Dictionary<string, List<int>>
        {
            [KoboldA] = [0, 2],
            [KoboldB] = [1]
        });
        plan.GetAssignedSteps(KoboldB).Select(s => s.Title).Should().Equal("Stop the game on a hit");
    }

    [Fact]
    public async Task A_stored_plan_in_Planning_loads_as_Created()
    {
        var plan = await LoadStoredAsync("world");

        plan.Status.Should().Be(PlanStatus.Created, "DraCode's Planning is Birko's Created, both stored as 0");
        plan.Steps.Should().HaveCount(2).And.OnlyContain(s => s.Status == StepStatus.Pending);
    }

    [Theory]
    [InlineData("chasecam")]
    [InlineData("collision")]
    [InlineData("world")]
    public void Every_legacy_plan_file_reads_with_the_plan_file_options(string name)
    {
        var plan = LegacyPlanJson.Deserialize(Fixture($"{name}-plan.json"), PlanFileOptions);

        plan.Should().NotBeNull();
        plan!.ProjectId.Should().Be("flappy-dragon");
        if (name == "chasecam") ShouldBeChasecam(plan);
    }

    [Fact]
    public void A_plan_saved_in_the_new_shape_round_trips_unchanged()
    {
        var plan = LegacyPlanJson.Deserialize(Fixture("chasecam.plan-data.json"), PlanFileOptions)!;
        var saved = JsonSerializer.Serialize(plan, PlanFileOptions);

        LegacyPlanJson.Upgrade(saved).Should().Be(saved, "nothing in the new shape is legacy");
        ShouldBeChasecam(LegacyPlanJson.Deserialize(saved, PlanFileOptions)!);
    }
}
